#if UNITY_EDITOR || PVZ_REMOTE
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Pvz.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pvz.Runtime.Remote
{
    /// <summary>
    /// Debug remote (T026): lets a computer play and inspect the game on a headset over adb
    /// («adb forward tcp:7777 tcp:7777», then one text command per line). Commands act like the automated tests do —
    /// press the menu buttons, scan a box, put it in the handover zone, play a whole shift — and the game answers with
    /// JSON state, PNG snapshots and frame sequences for videos. Compiled only in the editor and in debug builds
    /// (scripting define PVZ_REMOTE); a release APK has neither the code nor the open port.
    /// </summary>
    public sealed class PvzRemote : MonoBehaviour
    {
        public const int DefaultPort = 7777;

        /// <summary>A command from the socket waiting for the main thread. Either side claims it exactly once.</summary>
        private sealed class Pending
        {
            public const int Waiting = 0, Taken = 1, Abandoned = 2;
            public string Line;
            public string Reply;
            public int State;
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
        }

        /// <summary>How long the socket waits for the game (e.g. while the headset is taken off) before giving up.</summary>
        private static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(30);

        /// <summary>A connection that sends nothing for this long is dropped, so it cannot lock out the developer.</summary>
        private const int IdleTimeoutMs = 120000;

        /// <summary>Frames one recording may hold (~2.5 min at 12 fps), so a forgotten recording cannot fill the headset.</summary>
        public const int MaxRecordedFrames = 1800;

        private readonly ConcurrentQueue<Pending> _queue = new ConcurrentQueue<Pending>();
        private TcpListener _listener;
        private Thread _thread;
        private volatile bool _running;
        private volatile bool _paused;
        private float _fps = 60f;
        private Coroutine _autoplay;
        private Coroutine _recording;
        private int _recordedFrames;
        private string _recordDir;
        private string _view = "counter";

        public static PvzRemote Instance { get; private set; }

        /// <summary>The port the remote listens on (0 when it only executes commands in-process, e.g. in tests).</summary>
        public int Port { get; private set; }

        public bool IsAutoplaying => _autoplay != null;

#if PVZ_REMOTE && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart() => Launch(DefaultPort);
#endif

        /// <summary>Starts the remote (once per app; survives scene reloads). Port 0: no socket, in-process only.</summary>
        public static PvzRemote Launch(int port)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("PVZ Remote");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<PvzRemote>();
            if (port > 0) Instance.Listen(port);
            Debug.Log($"[PvzRemote] ready{(port > 0 ? $" on 127.0.0.1:{port}" : " (in-process)")}");
            return Instance;
        }

        /// <summary>
        /// Secret the first line of every connection must carry. Loopback is reachable by every app on the headset, not
        /// only by adb; the token lives in the app's own files (readable over adb, not by other apps).
        /// </summary>
        public string Token { get; private set; }

        public const string TokenFile = "remote-token.txt";

        private void Listen(int port)
        {
            Port = port;
            Token = Guid.NewGuid().ToString("N");
            File.WriteAllText(Path.Combine(Application.persistentDataPath, TokenFile), Token);
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();
            _running = true;
            _thread = new Thread(Serve) { IsBackground = true, Name = "PvzRemote" };
            _thread.Start();
        }

        private void OnApplicationPause(bool paused) => _paused = paused;

        private void OnDestroy()
        {
            _running = false;
            try { _listener?.Stop(); } catch (SocketException) { }
            StopRecording();
            if (_target != null) { _target.Release(); Destroy(_target); }
            if (_pixels != null) Destroy(_pixels);
            if (Instance == this) Instance = null;
        }

        /// <summary>Socket thread: checks the token, reads lines, hands them to the main thread, writes the replies back.</summary>
        private void Serve()
        {
            while (_running)
            {
                TcpClient client;
                try { client = _listener.AcceptTcpClient(); }
                catch (SocketException) { return; }
                catch (ObjectDisposedException) { return; }
                using (client)
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" })
                {
                    client.ReceiveTimeout = IdleTimeoutMs;
                    try
                    {
                        if (reader.ReadLine() != "token " + Token)
                        {
                            writer.WriteLine(RemoteProtocol.Error("first line must be «token <" + TokenFile + ">»"));
                            continue;
                        }
                        writer.WriteLine(RemoteProtocol.Ok("auth", true));
                        string line;
                        while (_running && (line = reader.ReadLine()) != null) writer.WriteLine(Answer(line));
                    }
                    catch (IOException) { } // idle timeout or the client went away
                }
            }
        }

        /// <summary>Waits for the main thread; a command it did not take in time is cancelled, never run later.</summary>
        private string Answer(string line)
        {
            if (_paused) return RemoteProtocol.Error("the game is paused (headset not worn?)");
            var pending = new Pending { Line = line };
            _queue.Enqueue(pending);
            string reply;
            if (pending.Done.Wait(AnswerTimeout)) reply = pending.Reply;
            else if (Interlocked.CompareExchange(ref pending.State, Pending.Abandoned, Pending.Waiting) == Pending.Waiting)
                reply = RemoteProtocol.Error("the game did not answer in 30 s; the command was cancelled");
            else
            {
                pending.Done.Wait(); // taken just in time: it is running, wait for its reply
                reply = pending.Reply;
            }
            pending.Done.Dispose();
            return reply;
        }

        private void Update()
        {
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.05f);
            while (_queue.TryDequeue(out var pending))
            {
                if (Interlocked.CompareExchange(ref pending.State, Pending.Taken, Pending.Waiting) != Pending.Waiting) continue;
                pending.Reply = Execute(pending.Line);
                pending.Done.Set();
            }
        }

        /// <summary>Runs one command on the main thread and returns the JSON reply (also used directly by tests).</summary>
        public string Execute(string line)
        {
            if (!RemoteProtocol.TryParse(line, out var name, out var args)) return RemoteProtocol.Error("empty command");
            try
            {
                switch (name)
                {
                    case "ping": return RemoteProtocol.Ok("pong", true);
                    case "help": return RemoteProtocol.Ok("commands", "ping state begin recenter theme restart code scan give bag " +
                                                                      "return move timescale area autoplay shot record view hand pc");
                    case "state": return State();
                    case "begin": return Begin(args.Length > 0 ? args[0] : "normal");
                    case "recenter": return Press<RecenterButton>(b => b.Press());
                    case "theme": return Press<ThemeButton>(b => b.Press());
                    case "restart":
                        StopAutoplay();
                        SceneManager.LoadScene(SceneManager.GetActiveScene().path);
                        return RemoteProtocol.Ok("restarting", true);
                    case "scan": return Scan(Arg(args, 0));
                    case "code": return ScanCode();
                    case "give": return Give(Arg(args, 0));
                    case "bag": return Bag();
                    case "return": return ScanReturn();
                    case "move": return Move(args);
                    case "timescale": return TimeScale(Arg(args, 0));
                    case "area": return Area(args);
                    case "autoplay": return Autoplay(args.Length == 0 || args[0] != "off");
                    case "view": return View(Arg(args, 0));
                    case "shot": return Shot(args.Length > 0 ? args[0] : "shot", args.Length > 1 ? args[1] : _view);
                    case "record": return Record(args);
                    case "hand": return Hand(args);
                    case "pc": return Computer(Arg(args, 0));
                    default: return RemoteProtocol.Error($"unknown command «{name}» (try help)");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                return RemoteProtocol.Error(e.GetType().Name + ": " + e.Message);
            }
        }

        private static string Arg(string[] args, int index) => index < args.Length ? args[index] : null;

        private static ShiftDirector Director => FindFirstObjectByType<ShiftDirector>();

        // ---------- state ----------

        private string State()
        {
            var director = Director;
            var board = FindFirstObjectByType<CounterBoard>(FindObjectsInactive.Include);
            var json = new Json()
                .Add("ok", true)
                .Add("time", Time.realtimeSinceStartup)
                .Add("fps", _fps)
                .Add("hz", DisplayRate.Current)
                .Add("timeScale", Time.timeScale)
                .Add("scene", SceneManager.GetActiveScene().name)
                .Add("autoplay", IsAutoplaying)
                .Add("recording", _recording != null)
                .Add("frames", _recordedFrames);
            if (board != null)
            {
                json.Add("headline", board.Headline).Add("status", board.Status).Add("footer", board.Footer).Add("orders", board.Orders);
            }
            var pc = FindFirstObjectByType<PvzComputer>();
            if (pc != null) json.Add("computer", pc.Current.ToString()).Add("screen", pc.ScreenText);
            foreach (var placement in FindObjectsByType<PlacementKeeper>(FindObjectsSortMode.None))
            {
                if (placement.Rig == null) continue;
                json.Add(placement.Rig.GetComponent<SupplyHatch>() != null ? "hatchAt" : placement.Rig.name == "Shelf Rack" ? "rackAt" : "counterAt",
                    placement.Current.Format());
            }
            var holder = FindFirstObjectByType<ScannerHolder>(); // T038
            if (holder != null) json.Add("scannerDocked", holder.IsDocked).Add("scannerReturns", holder.Returns);
            if (director == null) return json.Add("director", false).ToString();

            json.Add("started", director.IsStarted).Add("shiftOver", director.IsShiftOver)
                .Add("scanner", director.Scanner != null ? director.Scanner.ScreenText : null)
                .Add("customer", director.CurrentCustomer != null ? director.CurrentCustomer.LastLine : null);
            if (!director.IsStarted) return json.ToString();

            var shift = director.Shift;
            json.Add("difficulty", director.ActiveSettings.DisplayName)
                .Add("score", director.Stats.Score)
                .Add("served", director.Stats.OrdersCompleted)
                .Add("total", director.OrdersTotal)
                .Add("timeLeft", shift.TimeLeft(Time.timeAsDouble))
                .Add("queue", shift.QueuedVisits.Count);
            var desk = director.Desk;
            if (desk.HasActiveOrder)
            {
                var session = desk.ActiveSession;
                json.Add("order", new Json.Raw(new Json()
                    .Add("number", session.Order.Number)
                    .Add("boxes", new Json.Raw(Json.Array(session.Order.Boxes.Select(b => Json.Quote(b.Value)))))
                    .Add("left", session.RemainingCount)
                    .Add("awaitingBag", session.AwaitingBag)
                    .Add("urgent", session.Order.IsUrgent).ToString()));
            }
            if (desk.HasActiveReturn) json.Add("return", desk.ActiveReturn.Box.Value);
            json.Add("awaitsCode", desk.AwaitsCode);
            json.Add("boxes", new Json.Raw(Json.Array(director.RemainingBoxes.Where(b => b != null).Select(b =>
            {
                var p = b.transform.localPosition;
                return new Json().Add("id", b.RawId).Add("x", p.x).Add("y", p.y).Add("z", p.z)
                    .Add("pending", shift.IsPendingIntake(b.Id)).ToString();
            }))));
            return json.ToString();
        }

        // ---------- menu ----------

        private static string Begin(string difficulty)
        {
            var director = Director;
            if (director == null) return RemoteProtocol.Error("no shift director in the scene");
            if (director.IsStarted) return RemoteProtocol.Error("the shift has already started");
            // Buttons are built easy → normal → hard.
            var index = difficulty == "easy" ? 0 : difficulty == "hard" ? 2 : 1;
            var buttons = director.DifficultyButtons;
            if (index < buttons.Count && buttons[index] != null) buttons[index].Press();
            else director.Begin();
            return RemoteProtocol.Ok("difficulty", director.IsStarted ? director.ActiveSettings.DisplayName : null);
        }

        private static string Press<T>(Action<T> press) where T : Component
        {
            var button = FindFirstObjectByType<T>(FindObjectsInactive.Include); // keys of other computer screens are hidden
            if (button == null) return RemoteProtocol.Error($"no {typeof(T).Name} in the scene");
            var director = Director;
            var running = director != null && director.IsStarted && !director.IsShiftOver;
            if (running && button is RecenterButton)
                return RemoteProtocol.Error($"{typeof(T).Name} is a before-the-shift setting (the computer hides it during a shift)");
            press(button);
            return RemoteProtocol.Ok("pressed", typeof(T).Name);
        }

        // ---------- shift ----------

        private static bool TryBox(string id, out QrBox box, out string error)
        {
            box = null;
            error = null;
            var director = Director;
            if (string.IsNullOrEmpty(id)) error = "give a box id, e.g. BOX-001";
            else if (director == null || !director.IsStarted) error = "the shift has not started";
            else if (!director.TryGetBox(new BoxId(id), out box)) error = $"no box {id} in the warehouse";
            return error == null;
        }

        private static string Scan(string id)
        {
            if (!TryBox(id, out var box, out var error)) return RemoteProtocol.Error(error);
            Director.Scanner.ScanDirect(box);
            return RemoteProtocol.Ok("scanner", Director.Scanner.ScreenText);
        }

        /// <summary>code: scans the barcode on the phone of the customer at the counter (T035).</summary>
        private static string ScanCode()
        {
            var director = Director;
            var phone = director != null && director.CurrentCustomer != null ? director.CurrentCustomer.Phone : null;
            if (phone == null || !phone.IsShown) return RemoteProtocol.Error("no phone shown at the counter");
            director.Scanner.ScanDirect(phone);
            return RemoteProtocol.Ok("scanner", director.Scanner.ScreenText);
        }

        private static string ScanReturn()
        {
            var director = Director;
            if (director == null || director.CurrentReturnBox == null) return RemoteProtocol.Error("no return box on the counter");
            director.Scanner.ScanDirect(director.CurrentReturnBox);
            return RemoteProtocol.Ok("scanner", director.Scanner.ScreenText);
        }

        private static string Give(string id)
        {
            if (!TryBox(id, out var box, out var error)) return RemoteProtocol.Error(error);
            Teleport(box.transform, box.GetComponent<Rigidbody>(), Director.Zone.transform.position + Vector3.up * 0.05f);
            return RemoteProtocol.Ok("moved", id);
        }

        private static string Bag()
        {
            var dispenser = FindFirstObjectByType<BagDispenser>();
            if (dispenser == null || dispenser.Current == null) return RemoteProtocol.Error("no bag on the counter");
            Teleport(dispenser.Current.transform, dispenser.Current.GetComponent<Rigidbody>(),
                Director.Zone.transform.position + Vector3.up * 0.05f);
            return RemoteProtocol.Ok("moved", "bag");
        }

        private static string Move(string[] args)
        {
            if (args.Length < 4 || !RemoteProtocol.TryNumber(args[1], out var x) || !RemoteProtocol.TryNumber(args[2], out var y) ||
                !RemoteProtocol.TryNumber(args[3], out var z)) return RemoteProtocol.Error("move BOX-ID x y z (play-area metres)");
            if (!TryBox(args[0], out var box, out var error)) return RemoteProtocol.Error(error);
            var parent = box.transform.parent;
            var world = parent != null ? parent.TransformPoint(new Vector3(x, y, z)) : new Vector3(x, y, z);
            Teleport(box.transform, box.GetComponent<Rigidbody>(), world);
            return RemoteProtocol.Ok("moved", args[0]);
        }

        /// <summary>Puts an object somewhere as a hand would let it go there (no flying speed).</summary>
        private static void Teleport(Transform target, Rigidbody body, Vector3 position)
        {
            if (body != null && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            target.position = position;
            if (body != null) body.position = position;
            Physics.SyncTransforms();
        }

        /// <summary>pc settings|back|end: the keys of the counter computer (T033); without an argument — its screen.</summary>
        private static string Computer(string key)
        {
            var pc = FindFirstObjectByType<PvzComputer>();
            if (pc == null) return RemoteProtocol.Error("no computer in the scene");
            switch ((key ?? string.Empty).ToLowerInvariant())
            {
                case "settings": pc.OpenSettings(); break;
                case "back": pc.Back(); break;
                case "end": pc.EndShift(); break;
                case "move": pc.StartMovingCounter(); break;
                case "hatch": pc.StartMovingHatch(); break;
                case "rack": pc.StartMovingRack(); break;
                case "apply": pc.ApplyMove(); break;
                case "cancel": pc.CancelMove(); break;
                case "": break;
                default: return RemoteProtocol.Error("pc settings|back|end|move|hatch|rack|apply|cancel");
            }
            return new Json().Add("ok", true).Add("computer", pc.Current.ToString()).Add("screen", pc.ScreenText).ToString();
        }

        // ---------- virtual controller (real XRI input path) ----------

        private VirtualHand _hand;

        /// <summary>
        /// hand on|off · hand aim scanner|BOX-ID|button|x y z · hand grip on|off · hand trigger on|off · hand [debug] (status).
        /// </summary>
        private string Hand(string[] args)
        {
            if (_hand == null) _hand = gameObject.AddComponent<VirtualHand>();
            var verb = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            switch (verb)
            {
                case "on":
                    if (_hand.IsOn) break; // already on: never jump a hand that may hold something
                    _hand.TurnOn();
                    if (Camera.main != null) _hand.PointAt(Camera.main.transform.position + Camera.main.transform.forward);
                    _hand.Teleport(_hand.TargetPosition, _hand.TargetRotation); // nothing is held yet
                    break;
                case "off": _hand.TurnOff(); break;
                case "aim":
                    if (!_hand.IsOn) return RemoteProtocol.Error("hand on first");
                    if (!TryTarget(args, out var target, out var error, out var targetObject)) return RemoteProtocol.Error(error);
                    var held = FindFirstObjectByType<HandScanner>();
                    var holding = held != null && held.IsHeld;
                    // Aim from a spot with a clear line to the target (other boxes, the counter are not in the way).
                    if (!holding)
                    {
                        _hand.PointAt(target, 0.5f, targetObject);
                        break;
                    }
                    // Holding the scanner: the line is checked from where its muzzle will be, 0.5 m from the target (#93).
                    if (targetObject != null && targetObject.IsChildOf(held.transform))
                        return RemoteProtocol.Error("the scanner is already in the hand");
                    if (!_hand.AimHeldAt(held.Muzzle, target, targetObject, held.transform))
                        return RemoteProtocol.Error($"no clear line from the scanner to {args[1]} (something is in the way from every side)");
                    break;
                case "to":
                    if (!_hand.IsOn) return RemoteProtocol.Error("hand on first");
                    if (args.Length < 4 || !RemoteProtocol.TryNumber(args[1], out var tx) || !RemoteProtocol.TryNumber(args[2], out var ty) ||
                        !RemoteProtocol.TryNumber(args[3], out var tz)) return RemoteProtocol.Error("hand to x y z (metres, world)");
                    _hand.SetPose(new Vector3(tx, ty, tz), _hand.TargetRotation); // carry what is held, keep the hand turned as it is
                    break;
                case "grip":
                case "trigger":
                    if (!_hand.IsOn) return RemoteProtocol.Error("hand on first");
                    var on = Arg(args, 1) != "off";
                    if (verb == "grip") _hand.SetGrip(on); else _hand.SetTrigger(on);
                    break;
                case "": break;
                case "debug": return HandStatus(debug: true);
                default: return RemoteProtocol.Error("hand on|off|aim TARGET|grip on|off|trigger on|off");
            }
            return HandStatus();
        }

        private static string Vec(Vector3 v) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.00} {1:0.00} {2:0.00}", v.x, v.y, v.z);

        private static bool TryTarget(string[] args, out Vector3 target, out string error, out Transform targetObject)
        {
            target = default;
            error = null;
            targetObject = null;
            var what = Arg(args, 1);
            if (string.Equals(what, "phone", StringComparison.OrdinalIgnoreCase))
            {
                var director = Director;
                var phone = director != null && director.CurrentCustomer != null ? director.CurrentCustomer.Phone : null;
                if (phone == null || !phone.IsShown) { error = "no phone shown at the counter"; return false; }
                var hit = phone.GetComponentInChildren<Collider>();
                target = hit != null ? hit.bounds.center : phone.transform.position;
                targetObject = phone.transform;
                return true;
            }
            if (string.Equals(what, "handle", StringComparison.OrdinalIgnoreCase))
            {
                var mover = FindObjectsByType<PlacementMover>(FindObjectsSortMode.None)
                    .FirstOrDefault(m => m.Handle != null && m.Handle.gameObject.activeInHierarchy); // the counter's or the hatch's
                if (mover == null) { error = "no handle shown (pc settings, then pc move or pc hatch)"; return false; }
                target = mover.Handle.transform.position;
                targetObject = mover.Handle.transform;
                return true;
            }
            if (string.Equals(what, "scanner", StringComparison.OrdinalIgnoreCase))
            {
                var scanner = FindFirstObjectByType<HandScanner>();
                if (scanner == null) { error = "no scanner"; return false; }
                target = scanner.transform.position;
                targetObject = scanner.transform;
                return true;
            }
            if (args.Length >= 4 && RemoteProtocol.TryNumber(args[1], out var x) && RemoteProtocol.TryNumber(args[2], out var y) &&
                RemoteProtocol.TryNumber(args[3], out var z))
            {
                target = new Vector3(x, y, z);
                return true;
            }
            foreach (var button in FindObjectsByType<PressableButton>(FindObjectsSortMode.None))
            {
                if (string.IsNullOrEmpty(what) || button.name.IndexOf(what, StringComparison.OrdinalIgnoreCase) < 0) continue;
                target = button.transform.position; // a counter button by part of its name (hand aim Обычная, hand aim Recenter…)
                targetObject = button.transform;
                return true;
            }
            if (!TryBox(what, out var box, out error)) return false;
            target = box.transform.position;
            targetObject = box.transform;
            return true;
        }

        private string HandStatus(bool debug = false)
        {
            var selected = new List<string>();
            var hovered = new List<string>();
            var details = new List<string>();
            foreach (var interactor in FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>(FindObjectsSortMode.None))
            {
                if (!interactor.isActiveAndEnabled) continue;
                foreach (var i in interactor.interactablesSelected) selected.Add(interactor.name + ":" + i.transform.name);
                foreach (var i in interactor.interactablesHovered) hovered.Add(interactor.name + ":" + i.transform.name);
                if (!debug) continue;
                details.Add($"{interactor.name}@{Vec(interactor.transform.position)}>{Vec(interactor.transform.forward)}");
                if (interactor is UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor input)
                    details.Add($"{interactor.name}:selectActive={input.isSelectActive}");
            }
            var scanner = FindFirstObjectByType<HandScanner>();
            if (debug)
            {
                if (scanner != null) details.Add($"muzzle@{Vec(scanner.Muzzle.position)}>{Vec(scanner.Muzzle.forward)}");
                if (Camera.main != null) details.Add("head@" + Vec(Camera.main.transform.position));
                var manager = FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>();
                if (manager != null)
                    foreach (var asset in manager.actionAssets)
                    {
                        var map = asset != null ? asset.FindActionMap("XRI Right Interaction") : null;
                        if (map == null) continue;
                        foreach (var a in new[] { "Select", "Activate" })
                        {
                            var action = map.FindAction(a);
                            if (action != null) details.Add($"{a}:phase={action.phase},pressed={action.IsPressed()}");
                        }
                    }
            }
            var json = new Json().Add("ok", true).Add("hand", _hand != null && _hand.IsOn);
            if (_hand != null && _hand.IsOn)
            {
                json.Add("grip", _hand.Grip).Add("trigger", _hand.Trigger).Add("pos", Vec(_hand.WorldPosition)).Add("moving", _hand.IsMoving || _hand.IsAiming);
                if (debug) json.Add("device", _hand.DeviceReport);
            }
            json.Add("mode", UnityEngine.XR.Interaction.Toolkit.Inputs.XRInputModalityManager.currentInputMode.Value.ToString())
                .Add("selected", string.Join(", ", selected)).Add("hovered", string.Join(", ", hovered));
            if (scanner != null) json.Add("scanner", scanner.ScreenText).Add("lastHit", scanner.LastHit);
            if (debug) json.Add("debug", string.Join(", ", details));
            return json.ToString();
        }

        /// <summary>
        /// Shifts the whole play area (counter, boxes, customers) in its own frame: right, up, forward, metres. For filming
        /// with a headset lying on a table: lower the area and move it away to see the counter as a standing player does.
        /// </summary>
        private static string Area(string[] args)
        {
            if (args.Length < 3 || !RemoteProtocol.TryNumber(args[0], out var right) || !RemoteProtocol.TryNumber(args[1], out var up) ||
                !RemoteProtocol.TryNumber(args[2], out var forward)) return RemoteProtocol.Error("area right up forward (metres)");
            var session = FindFirstObjectByType<MrSessionSetup>();
            var area = session != null ? session.PlayArea : null;
            if (area == null) return RemoteProtocol.Error("no play area");
            area.position += area.right * right + Vector3.up * up + area.forward * forward;
            Physics.SyncTransforms();
            var p = area.position;
            return new Json().Add("ok", true).Add("x", p.x).Add("y", p.y).Add("z", p.z).ToString();
        }

        private static string TimeScale(string value)
        {
            if (!RemoteProtocol.TryNumber(value, out var scale) || scale < 0f || scale > 10f) return RemoteProtocol.Error("timescale 0..10");
            Time.timeScale = scale;
            return RemoteProtocol.Ok("timeScale", scale);
        }

        // ---------- autoplay ----------

        private string Autoplay(bool on)
        {
            StopAutoplay();
            if (!on) return RemoteProtocol.Ok("autoplay", false);
            var director = Director;
            if (director == null || director.IsShiftOver) return RemoteProtocol.Error("no shift to play (restart first)");
            _autoplay = StartCoroutine(PlayShift());
            return RemoteProtocol.Ok("autoplay", true);
        }

        private void StopAutoplay()
        {
            if (_autoplay != null) StopCoroutine(_autoplay);
            _autoplay = null;
        }

        /// <summary>
        /// Plays the shift like a calm player: accepts deliveries, scans every box of the order and puts it in the zone,
        /// hands over a bag when asked, accepts returns. Pauses keep it watchable on video.
        /// </summary>
        private IEnumerator PlayShift()
        {
            var pause = new WaitForSeconds(0.7f);
            while (true)
            {
                var director = Director;
                if (director == null || director.IsShiftOver) break;
                if (!director.IsStarted)
                {
                    yield return null;
                    continue;
                }

                foreach (var supply in director.RemainingBoxes.Where(b => b != null && director.Shift.IsPendingIntake(b.Id)).ToArray())
                {
                    director.Scanner.ScanDirect(supply);
                    yield return pause;
                }

                var desk = director.Desk;
                if (desk.AwaitsCode && director.CurrentCustomer != null && !director.CurrentCustomer.IsWalking)
                {
                    // T035: the code on the phone first (a customer without a phone: the code is taken as read).
                    if (director.CurrentCustomer.Phone != null) director.Scanner.ScanDirect(director.CurrentCustomer.Phone);
                    else director.Shift.ScanCustomerCode();
                    yield return pause;
                    continue;
                }
                if (desk.HasActiveReturn && director.CurrentReturnBox != null)
                {
                    director.Scanner.ScanDirect(director.CurrentReturnBox);
                    yield return pause;
                }
                else if (desk.HasActiveOrder)
                {
                    var session = desk.ActiveSession;
                    // Still the same customer? (They may run out of patience and the next one step up while we wait.)
                    bool SameCustomer() => desk.HasActiveOrder && desk.ActiveSession == session;
                    foreach (var id in session.Order.Boxes.ToArray())
                    {
                        if (!SameCustomer()) break;
                        if (session.IsDelivered(id) || !director.TryGetBox(id, out var box)) continue;
                        director.Scanner.ScanDirect(box);
                        yield return pause;
                        if (!SameCustomer()) break;
                        Teleport(box.transform, box.GetComponent<Rigidbody>(), director.Zone.transform.position + Vector3.up * 0.05f);
                        yield return pause;
                    }
                    if (SameCustomer() && session.AwaitingBag)
                    {
                        Bag();
                        yield return pause;
                    }
                }
                yield return null;
            }
            _autoplay = null;
        }

        // ---------- pictures ----------

        private string View(string view)
        {
            if (view != "head" && view != "overview" && view != "counter" && view != "room")
                return RemoteProtocol.Error("view head|counter|overview|room");
            _view = view;
            return RemoteProtocol.Ok("view", view);
        }

        /// <summary>Where the picture is taken from: the player's eyes, the counter, or above the room.</summary>
        private static void Pose(string view, out Vector3 position, out Vector3 lookAt)
        {
            var head = Camera.main != null ? Camera.main.transform : null;
            var session = FindFirstObjectByType<MrSessionSetup>();
            var area = session != null ? session.PlayArea : null;
            Vector3 Local(Vector3 p) => area != null ? area.TransformPoint(p) : p;
            switch (view)
            {
                case "head" when head != null:
                    position = head.position;
                    lookAt = head.position + head.forward;
                    break;
                case "overview":
                    position = Local(new Vector3(-2.6f, 2.5f, -2.9f));
                    lookAt = Local(new Vector3(0.1f, 0.8f, 0.6f));
                    break;
                case "room":
                    position = Local(new Vector3(0f, 4.5f, -0.5f));
                    lookAt = Local(new Vector3(0f, 0f, 0.3f));
                    break;
                default: // the counter with the board above it, from where the player stands
                    position = Local(new Vector3(0.1f, 1.75f, -0.75f));
                    lookAt = Local(new Vector3(0.25f, 1.55f, 1.2f));
                    break;
            }
        }

        private Camera _camera;
        private RenderTexture _target;
        private Texture2D _pixels;

        /// <summary>Renders the scene from a view into an image (the real room is not visible: passthrough is not rendered here).</summary>
        private byte[] Capture(string view, int width, int height, bool jpg)
        {
            if (_camera == null)
            {
                var go = new GameObject("PVZ Remote Camera");
                go.transform.SetParent(transform, false);
                _camera = go.AddComponent<Camera>();
                _camera.enabled = false;
                _camera.stereoTargetEye = StereoTargetEyeMask.None;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0.16f, 0.18f, 0.22f);
                _camera.nearClipPlane = 0.02f;
                _camera.fieldOfView = 70f;
            }
            if (_target == null || _target.width != width || _target.height != height)
            {
                // One pair of textures at a time: the old ones are destroyed, not only released (GPU and CPU memory).
                if (_target != null) { _target.Release(); Destroy(_target); }
                if (_pixels != null) Destroy(_pixels);
                _target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
                _pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            }
            Pose(view, out var position, out var lookAt);
            _camera.transform.position = position;
            _camera.transform.LookAt(lookAt);
            _camera.targetTexture = _target;
            _camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = _target;
            _pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            _pixels.Apply();
            RenderTexture.active = previous;
            _camera.targetTexture = null;
            return jpg ? _pixels.EncodeToJPG(80) : _pixels.EncodeToPNG();
        }

        private static string MediaDir(string kind)
        {
            var dir = Path.Combine(Application.persistentDataPath, "pvz-media", kind);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private string Shot(string name, string view)
        {
            var safe = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
            if (safe.Length == 0) safe = "shot";
            var path = Path.Combine(MediaDir("shots"), safe + ".png");
            File.WriteAllBytes(path, Capture(view, 1600, 900, false));
            return RemoteProtocol.Ok("path", path);
        }

        private string Record(string[] args)
        {
            var action = Arg(args, 0);
            if (action == "stop")
            {
                StopRecording();
                return new Json().Add("ok", true).Add("frames", _recordedFrames).Add("dir", _recordDir).ToString();
            }
            if (action != "start") return RemoteProtocol.Error("record start [fps] [view] | record stop");
            var fps = RemoteProtocol.TryNumber(Arg(args, 1), out var f) ? Mathf.Clamp(f, 1f, 30f) : 12f;
            var view = Arg(args, 2) ?? _view;
            StopRecording();
            // Sortable and unique: date, time and a counter (two recordings may start within one second).
            _recordDir = Path.Combine(MediaDir("rec"), DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + (++_recordings).ToString("00"));
            Directory.CreateDirectory(_recordDir);
            _recordedFrames = 0;
            _recording = StartCoroutine(RecordFrames(fps, view));
            return new Json().Add("ok", true).Add("dir", _recordDir).Add("fps", fps).ToString();
        }

        private int _recordings;

        private void StopRecording()
        {
            if (_recording != null) StopCoroutine(_recording);
            _recording = null;
        }

        /// <summary>
        /// Frames for a video from the game's camera. Each frame is rendered and read back on the main thread, which costs
        /// frame time on the headset — for videos of the real room use the headset's own recording (tools/pvz-record.ps1).
        /// </summary>
        private IEnumerator RecordFrames(float fps, string view)
        {
            var wait = new WaitForSecondsRealtime(1f / fps);
            while (_recordedFrames < MaxRecordedFrames)
            {
                _recordedFrames++;
                File.WriteAllBytes(Path.Combine(_recordDir, $"f_{_recordedFrames:00000}.jpg"), Capture(view, 960, 540, true));
                yield return wait;
            }
            _recording = null; // the cap was reached
        }
    }
}
#endif
