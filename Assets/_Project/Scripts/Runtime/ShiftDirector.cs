using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Pvz.Core;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// Composition root and pacing of a shift: creates the Core <see cref="Shift"/>, forwards player input
    /// (scanner, handover zone) to it, walks customers in and out. Rules live in Core, texts and sounds in
    /// <see cref="ShiftPresenter"/>.
    /// </summary>
    public sealed class ShiftDirector : MonoBehaviour
    {
        [SerializeField] private ShiftSettings settings;
        [SerializeField] private HandScanner scanner;
        [SerializeField] private HandoverZone handoverZone;
        [SerializeField] private Customer customerPrefab;
        [SerializeField] private Transform customerSpawnPoint;
        [SerializeField] private Transform customerServicePoint;
        [SerializeField] private CounterBoard board;
        [SerializeField] private ShiftSummaryPanel summaryPanel;
        [SerializeField] private GameObject restartButton;
        [SerializeField] private FeedbackAudio feedback;
        [Tooltip("The supply hatch: the stock of every shift (and deliveries) drop out of it into a pile (T036).")]
        [SerializeField] private SupplyHatch hatch;
        [Tooltip("Optional: buttons to pick the difficulty. Without them the shift starts at once with the default settings.")]
        [SerializeField] private DifficultyButton[] difficultyButtons = Array.Empty<DifficultyButton>();
        [Tooltip("Optional: everything shown before the shift (difficulty buttons, recenter button); hidden on start.")]
        [SerializeField] private GameObject startMenu;
        [Tooltip("Generic box prefab carried by customers who come to return something.")]
        [SerializeField] private QrBox returnBoxPrefab;
        [Tooltip("Optional: confetti over the handover zone when an order is done.")]
        [SerializeField] private FeedbackEffects effects;
        [Tooltip("Optional: the career between shifts (T022); a finished shift reports its score to it.")]
        [SerializeField] private CareerKeeper career;

        private readonly BoxRegistry _registry = new BoxRegistry();
        private BestScores _bestScores;
        private readonly List<QueuedCustomer> _line = new List<QueuedCustomer>();
        private ShiftPresenter _presenter;
        private int _customersSpawned;
        private int _supplySeed;
        private bool _subscribed;

        public Shift Shift { get; private set; }
        public PickupDesk Desk => Shift.Desk;
        public ShiftStats Stats => Shift.Stats;
        public int OrdersTotal => Shift.OrdersTotal;
        public ShiftSettings ActiveSettings => settings;
        public bool IsStarted => Shift != null;
        public Customer CurrentCustomer { get; private set; }
        public bool IsShiftOver { get; private set; }
        public HandScanner Scanner => scanner;
        public HandoverZone Zone => handoverZone;
        public IEnumerable<QrBox> RemainingBoxes => _registry.All;
        /// <summary>Orders of the customers standing in line (the first one is at the counter).</summary>
        public IReadOnlyList<Order> QueuedOrders => Shift.QueuedOrders;
        public IReadOnlyList<Visit> QueuedVisits => Shift.QueuedVisits;
        /// <summary>The box a returning customer put on the counter (null when no return is being processed).</summary>
        public QrBox CurrentReturnBox { get; private set; }

        /// <summary>Sets how often customers come to return a box (tests).</summary>
        internal void UseReturnChance(int percent) => Shift.ReturnChancePercent = percent;

        /// <summary>Sets the shift length, counted from the start of the shift (tests; 0 = no limit).</summary>
        internal void UseTimeLimit(double seconds) => Shift.TimeLimitSeconds = seconds;

        /// <summary>Sets how often customers are in a hurry (tests).</summary>
        internal void UseEventChances(int urgentPercent) => Shift.UrgentChancePercent = urgentPercent;

        /// <summary>
        /// Tests that are not about the customer's code: it is read as soon as the customer reaches the counter, as if the
        /// player scanned the phone at once (T035).
        /// </summary>
        internal void UseAutoCustomerCode(bool on = true) => _autoCustomerCode = on;

        private bool _autoCustomerCode;

        public IReadOnlyList<DifficultyButton> DifficultyButtons => difficultyButtons;

        public event Action ShiftEnded;

        /// <summary>Raised once the shift has started (after the difficulty was chosen).</summary>
        public event Action ShiftStarted;

        /// <summary>
        /// «Завершить смену» (T033): the shift ends as if the time ran out — the line goes home, the customer at the
        /// counter is served to the end. Ignored before the start and after the end.
        /// </summary>
        public void EndShiftNow()
        {
            if (Shift == null || IsShiftOver || Shift.IsTimeUp) return;
            Shift.EndNow(Now);
        }

        private static double Now => Time.timeAsDouble;

        private void Awake()
        {
            if (!ValidateReferences()) enabled = false;
            _bestScores = new BestScores(new PlayerPrefsScoreStore());
        }

        private void OnEnable()
        {
            foreach (var button in difficultyButtons) if (button != null) button.Chosen += Begin;
            if (counterPlacement != null) counterPlacement.Moved += RepositionLine;
            if (Shift != null) Subscribe();
        }

        private void OnDisable()
        {
            foreach (var button in difficultyButtons) if (button != null) button.Chosen -= Begin;
            if (counterPlacement != null) counterPlacement.Moved -= RepositionLine;
            Unsubscribe();
        }

        private void OnDestroy()
        {
            _presenter?.Dispose();
            Shift?.Dispose();
        }

        private void Start()
        {
            if (Shift != null) return; // already started (e.g. by a test) before the first frame
            summaryPanel.Hide();
            restartButton.SetActive(false);
            if (HasDifficultyMenu)
                ShiftPresenter.ShowDifficultyMenu(board, MenuRecords(), career != null ? career.MenuText() : null);
            else Begin();
        }

        private void Update()
        {
            if (Shift == null || IsShiftOver) return; // the clock and patience stop with the shift
            Shift.Tick(Now);
            _presenter.UpdatePatience(Now);
            _presenter.UpdateClock(Now);
        }

        private bool HasDifficultyMenu => difficultyButtons.Any(b => b != null);

        /// <summary>Starts the shift with the default settings.</summary>
        public void Begin() => Begin(settings);

        /// <summary>Starts the shift with the chosen difficulty. Ignored once a shift is running.</summary>
        public void Begin(ShiftSettings chosen)
        {
            if (Shift != null || !isActiveAndEnabled) return;
            summaryPanel.Hide();
            restartButton.SetActive(false);
            if (chosen != null) settings = chosen;

            // T036: the warehouse is what drops out of the hatch — the shift's orders and a few spare boxes (other days' orders).
            var catalog = new BoxCatalog();
            var pile = new List<string>();
            foreach (var definition in settings.Boxes.Concat(settings.SpareBoxes))
            {
                if (string.IsNullOrEmpty(definition.boxId) || pile.Contains(definition.boxId)) continue;
                catalog.Add(new BoxRecord(new BoxId(definition.boxId), definition.orderNumber));
                pile.Add(definition.boxId);
            }

            Shift = new Shift(catalog, settings.CreateRandomSource(), settings.CreateScoringRules(), settings.CreateSupplyPlan(),
                settings.BagChancePercent) {
                PatienceSeconds = settings.PatienceSeconds,
                QueueLength = settings.QueueLength,
                ReturnChancePercent = settings.ReturnChancePercent,
                UrgentChancePercent = settings.UrgentChancePercent,
                UrgentPatienceFactor = settings.UrgentPatienceFactor,
                RequiresCustomerCode = true, // T035: the customer shows a barcode on the phone first
                TimeLimitSeconds = settings.ShiftDurationSeconds,
            };
            Shift.MarkNotForSale(settings.SpareBoxes.Select(b => b.orderNumber)); // nobody comes for the spare boxes
            Shift.Start(Now);
            if (Shift.ReturnChancePercent > 0 && returnBoxPrefab == null)
            {
                Debug.LogError("[ShiftDirector] Returns are enabled but there is no return box prefab; returns are turned off. Rebuild the scene.", this);
                Shift.ReturnChancePercent = 0;
            }
            _presenter = new ShiftPresenter(Shift, board, scanner, feedback, effects, settings.ShowTutorial);
            _supplySeed = settings.CreateRandomSource().Range(1, int.MaxValue);
            Subscribe();
            DropPile(pile);

            foreach (var button in difficultyButtons) if (button != null) button.gameObject.SetActive(false);
            if (startMenu != null) startMenu.SetActive(false);
            _presenter.ShowShiftStarting(settings.DisplayName);
            StartCoroutine(RunShift());
            ShiftStarted?.Invoke();
        }

        /// <summary>The stock of the shift falls out of the hatch in a mixed order (spares among the orders).</summary>
        private void DropPile(List<string> ids)
        {
            if (hatch == null)
            {
                Debug.LogError("[ShiftDirector] No supply hatch: the shift has no boxes. Rebuild the scene.", this);
                return;
            }
            var random = new System.Random(_supplySeed);
            for (var i = ids.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (ids[i], ids[j]) = (ids[j], ids[i]);
            }
            StartCoroutine(hatch.Drop(ids, _supplySeed, settings.DefaultCeilingHeight));
        }

        private void OnBoxDropped(QrBox box)
        {
            if (box != null) _registry.Register(box);
        }

        /// <summary>The hatch the stock falls out of (T036).</summary>
        public SupplyHatch Hatch => hatch;

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            scanner.BoxScanned += OnBoxScanned;
            scanner.Missed += OnScanMissed;
            if (hatch != null) hatch.BoxDropped += OnBoxDropped;
            scanner.PhoneScanned += OnPhoneScanned;
            Desk.CodeScanned += OnCodeScanned;
            Shift.OrderAbandoned += OnOrderAbandoned;
            handoverZone.BoxEntered += OnBoxEnteredZone;
            handoverZone.BagEntered += OnBagEnteredZone;
            Desk.BagRequested += OnBagRequested;
            Desk.ReturnOpened += OnReturnOpened;
            Shift.TimeUp += OnTimeUp;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            scanner.BoxScanned -= OnBoxScanned;
            scanner.Missed -= OnScanMissed;
            if (hatch != null) hatch.BoxDropped -= OnBoxDropped;
            scanner.PhoneScanned -= OnPhoneScanned;
            Desk.CodeScanned -= OnCodeScanned;
            Shift.OrderAbandoned -= OnOrderAbandoned;
            handoverZone.BoxEntered -= OnBoxEnteredZone;
            handoverZone.BagEntered -= OnBagEnteredZone;
            Desk.BagRequested -= OnBagRequested;
            Desk.ReturnOpened -= OnReturnOpened;
            Shift.TimeUp -= OnTimeUp;
        }

        /// <summary>Fixes the order sequence (tests). Call before the first customer arrives.</summary>
        internal void UseRandomSeed(int seed)
        {
            Shift.UseRandomSource(new SystemRandomSource(seed));
            _supplySeed = seed;
        }

        /// <summary>Sets how often customers ask for a bag (tests).</summary>
        internal void UseBagChance(int percent) => Shift.BagChancePercent = percent;

        /// <summary>Replaces where best scores are kept (tests: in memory instead of PlayerPrefs).</summary>
        internal void UseScoreStore(IScoreStore store) => _bestScores = new BestScores(store);

        public BestScores BestScores => _bestScores;

        private IEnumerable<(string name, int best)> MenuRecords()
        {
            var records = new List<(string name, int best)>();
            foreach (var button in difficultyButtons)
            {
                if (button == null || button.Settings == null) continue;
                try { records.Add((button.Settings.DisplayName, _bestScores.Get(button.Settings.DifficultyId))); }
                catch (ArgumentException e) { Debug.LogException(e, button); }
            }
            return records;
        }

        /// <summary>Sets how long customers wait (tests).</summary>
        internal void UsePatience(double seconds) => Shift.PatienceSeconds = seconds;

        public bool TryGetBox(BoxId id, out QrBox box) => _registry.TryGet(id, out box);

        private IEnumerator RunShift()
        {
            yield return new WaitUntil(() => hatch == null || !hatch.IsDropping); // the pile is down before anyone comes
            yield return new WaitForSeconds(settings.FirstCustomerDelay);
            while (!Shift.IsOver)
            {
                if (Shift.TryTakeDueDelivery(out var delivery))
                {
                    SpawnDelivery(delivery);
                    yield return new WaitForSeconds(settings.DelayBetweenCustomers);
                    continue;
                }

                yield return FillQueue();
                if (Shift.IsOver) break; // the time ran out while the line was filling
                if (_line.Count > 0)
                {
                    yield return ServeFrontCustomer();
                    if (Shift.IsOver) break;
                    _presenter.ShowWaitingForCustomer();
                    yield return new WaitForSeconds(settings.DelayBetweenCustomers);
                }
                else
                {
                    // Nothing to sell until the player accepts the delivery.
                    _presenter.ShowReceiveSupplyHint();
                    yield return new WaitUntil(() => Shift.HasUnreservedOrders || Shift.IsDeliveryDue || Shift.IsOver);
                }
            }
            EndShift();
        }

        private void SpawnDelivery(IReadOnlyList<BoxRecord> delivery)
        {
            if (hatch == null)
            {
                Debug.LogError("[ShiftDirector] A delivery arrived but there is no supply hatch in the scene.", this);
                return;
            }
            StartCoroutine(hatch.Drop(delivery.Select(b => b.Id.Value).ToArray(), _supplySeed, settings.DefaultCeilingHeight));
        }

        /// <summary>
        /// Brings new customers one by one (so they do not walk inside each other) until the line is full.
        /// Who comes and why (an order or a return) is decided by Core (<see cref="Shift.TryEnqueue"/>).
        /// </summary>
        private IEnumerator FillQueue()
        {
            while (Shift.TryEnqueue(out var visit))
            {
                var customer = Instantiate(customerPrefab, customerSpawnPoint.position, customerSpawnPoint.rotation);
                customer.SetLook(_customersSpawned++);
                _line.Add(new QueuedCustomer(customer, visit, visit.IsReturn ? SpawnReturnBox(customer, visit.Return) : null));
                customer.GoTo(SlotPosition(_line.Count - 1), customerServicePoint.rotation, settings.CustomerWalkSpeed);
                if (Shift.CanEnqueue) yield return new WaitForSeconds(QueueSpacing / settings.CustomerWalkSpeed + 0.2f);
            }
        }

        /// <summary>The box a returning customer carries: a generic box given the returned id and its QR.</summary>
        private QrBox SpawnReturnBox(Customer customer, ReturnRequest request)
        {
            var box = Instantiate(returnBoxPrefab, customer.CarryAnchor.position, customer.CarryAnchor.rotation);
            box.AssignId(request.Box.Value, QrTextures.Create(request.Box.Value));
            customer.Take(box);
            return box;
        }

        private IEnumerator ServeFrontCustomer()
        {
            var front = _line[0];
            CurrentCustomer = front.Customer;
            _presenter.Customer = front.Customer;
            _presenter.ShowCustomerApproaching();
            yield return new WaitUntil(() => front.Customer == null || !front.Customer.IsWalking || !IsStillQueued(front));
            if (!IsStillQueued(front))
            {
                // The shift time ran out on the way: OnTimeUp already sent this customer home.
                _presenter.Customer = null;
                CurrentCustomer = null;
                yield break;
            }
            if (front.Customer == null)
            {
                // The customer vanished on the way (should not happen): free their order for someone else.
                Shift.LeaveQueue(front.Visit);
                _line.RemoveAt(0);
                yield break;
            }

            Shift.OpenNext(Now); // a return puts its box on the counter (OnReturnOpened)
            if (Desk.AwaitsCode)
            {
                // T035: the customer holds out the phone; the order opens when its barcode is scanned (OnCodeScanned).
                if (front.Customer.Phone != null) front.Customer.Phone.Show(Desk.HasActiveReturn ? "КОД ВОЗВРАТА" : "КОД ЗАКАЗА");
                if (_autoCustomerCode) Shift.ScanCustomerCode();
            }
            // Boxes the player prepared before the customer arrived are handed over at once (outside the
            // OrderOpened event, so Core never re-enters itself).
            else if (Desk.HasActiveOrder) HandOverBoxesAlreadyInZone();

            yield return new WaitUntil(() => !Desk.HasCustomer);
            if (front.ReturnBox != null) Destroy(front.ReturnBox.gameObject); // taken back and written off
            CurrentReturnBox = null;
            yield return new WaitForSeconds(settings.ThanksDuration);

            _line.RemoveAll(q => q.Visit == front.Visit);
            _presenter.Customer = null;
            CurrentCustomer = null;
            // Leave sideways so the customer does not walk through the line; boxes go with them.
            front.Customer.LeaveAndVanish(ExitPosition, settings.CustomerWalkSpeed);
            for (var i = 0; i < _line.Count; i++)
            {
                if (_line[i].Customer != null)
                    _line[i].Customer.GoTo(SlotPosition(i), customerServicePoint.rotation, settings.CustomerWalkSpeed);
            }
        }

        private bool IsStillQueued(QueuedCustomer queued) => Shift.QueuedVisits.Contains(queued.Visit);

        /// <summary>The shift time ran out: everybody still waiting in line goes home (Core already dropped their visits).</summary>
        private void OnTimeUp(IReadOnlyList<Visit> sentAway)
        {
            foreach (var queued in _line.Where(q => sentAway.Contains(q.Visit)).ToArray())
            {
                _line.Remove(queued);
                if (queued.Customer != null) queued.Customer.LeaveAndVanish(ExitPosition, settings.CustomerWalkSpeed);
            }
        }

        private Vector3 LineDirection
        {
            get
            {
                var direction = customerSpawnPoint.position - customerServicePoint.position;
                direction.y = 0f;
                return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            }
        }

        /// <summary>Place number <paramref name="index"/> in the line (0 = at the counter).</summary>
        private Vector3 SlotPosition(int index) => customerServicePoint.position + LineDirection * (QueueSpacing * index);

        private Vector3 ExitPosition => customerServicePoint.position + Vector3.Cross(Vector3.up, LineDirection) * 2.2f
                                        + LineDirection * 0.8f;

        private const float QueueSpacing = 0.65f;

        private readonly struct QueuedCustomer
        {
            public QueuedCustomer(Customer customer, Visit visit, QrBox returnBox)
            {
                Customer = customer;
                Visit = visit;
                ReturnBox = returnBox;
            }

            public Customer Customer { get; }
            public Visit Visit { get; }
            /// <summary>The box a returning customer brought (null for pickups).</summary>
            public QrBox ReturnBox { get; }
        }

        private void EndShift()
        {
            IsShiftOver = true;
            _presenter.ShowShiftOver();
            RecordResult? record = null;
            string careerLine = null;
            try
            {
                var result = career != null ? career.AddShift(Stats.Score, settings.CareerPointsPercent) : null;
                if (result.HasValue) careerLine = career.SummaryText(result.Value);
            }
            catch (Exception e) { Debug.LogException(e, this); }
            try { record = _bestScores.Submit(settings.DifficultyId, Stats.Score); }
            catch (Exception e) { Debug.LogException(e, this); } // never leave the player without the summary and restart
            summaryPanel.Show(Stats, OrdersTotal, record, settings.DisplayName, Shift.MaxQueueLength, Shift.IsTimeUp && !Shift.Clock.EndedEarly,
                careerLine);
            restartButton.SetActive(true);
            ShiftEnded?.Invoke();
        }

        private void OnBoxScanned(QrBox box)
        {
            if (box != null && !IsShiftOver) Shift.Scan(box.Id); // after the summary nothing scores any more
        }

        private void OnScanMissed() => _presenter.ShowScanMissed();

        /// <summary>The scanner read a phone: the code of the customer at the counter opens their order (T035).</summary>
        private void OnPhoneScanned(CustomerPhone phone)
        {
            if (IsShiftOver || phone == null) return;
            if (CurrentCustomer == null || phone.Owner != CurrentCustomer || !Desk.AwaitsCode)
            {
                _presenter.ShowNotThisCode();
                return;
            }
            Shift.ScanCustomerCode();
        }

        /// <summary>The customer gave up (also before their code was scanned): the phone goes away at once.</summary>
        private void OnOrderAbandoned(Order order)
        {
            if (CurrentCustomer != null && CurrentCustomer.Phone != null) CurrentCustomer.Phone.Hide();
        }

        /// <summary>The code is read: the phone goes away, boxes already waiting in the zone are handed over.</summary>
        private void OnCodeScanned()
        {
            if (CurrentCustomer != null && CurrentCustomer.Phone != null) CurrentCustomer.Phone.Hide();
            if (Desk.HasActiveOrder) HandOverBoxesAlreadyInZone();
        }

        /// <summary>The returning customer at the counter puts their box next to the handover zone to be scanned.</summary>
        private void OnReturnOpened(ReturnRequest request)
        {
            var queued = _line.Find(q => q.Visit.Return == request);
            if (queued.ReturnBox == null) return;
            var spot = handoverZone.ReturnSpot;
            queued.ReturnBox.PresentForScan(spot.position, spot.rotation);
            CurrentReturnBox = queued.ReturnBox;
        }

        private void OnBoxEnteredZone(QrBox box)
        {
            if (IsShiftOver) return;
            var outcome = Desk.Handover(box.Id, Now);
            if (outcome.Status == HandoverStatus.Accepted) GiveToCustomer(box);
        }

        private void OnBagEnteredZone(ParcelBag bag)
        {
            if (IsShiftOver) return;
            if (Desk.HandOverBag(Now) == BagStatus.Accepted) GiveToCustomer(bag);
        }

        /// <summary>The customer asks for a bag: one may already be waiting in the zone.</summary>
        private void OnBagRequested(Order order)
        {
            foreach (var bag in handoverZone.BagsInside)
            {
                if (Desk.HandOverBag(Now) == BagStatus.Accepted) GiveToCustomer(bag);
                break;
            }
        }

        private void GiveToCustomer(ParcelBag bag)
        {
            handoverZone.Forget(bag);
            if (CurrentCustomer != null) CurrentCustomer.Take(bag);
        }

        private void HandOverBoxesAlreadyInZone()
        {
            var waiting = handoverZone.BoxesInside;
            var outcomes = Shift.HandOverWaiting(waiting.Select(b => b.Id), Now);
            foreach (var outcome in outcomes)
            {
                if (outcome.Status == HandoverStatus.Accepted && _registry.TryGet(outcome.Box, out var box)) GiveToCustomer(box);
            }
        }

        private void GiveToCustomer(QrBox box)
        {
            handoverZone.Forget(box);
            _registry.Unregister(box.Id);
            if (CurrentCustomer != null) CurrentCustomer.Take(box);
        }

        private bool ValidateReferences()
        {
            var missing = new List<string>();
            if (settings == null) missing.Add(nameof(settings));
            if (scanner == null) missing.Add(nameof(scanner));
            if (handoverZone == null) missing.Add(nameof(handoverZone));
            if (customerPrefab == null) missing.Add(nameof(customerPrefab));
            if (customerSpawnPoint == null) missing.Add(nameof(customerSpawnPoint));
            if (customerServicePoint == null) missing.Add(nameof(customerServicePoint));
            if (board == null) missing.Add(nameof(board));
            if (summaryPanel == null) missing.Add(nameof(summaryPanel));
            if (restartButton == null) missing.Add(nameof(restartButton));
            if (missing.Count == 0) return true;
            Debug.LogError($"[ShiftDirector] Missing references: {string.Join(", ", missing)}. Rebuild the scene (PVZ → Build Scene And Assets).", this);
            return false;
        }

        internal void ConfigureStartMenu(GameObject menuRoot) => startMenu = menuRoot;

        internal void ConfigureReturns(QrBox boxPrefab) => returnBoxPrefab = boxPrefab;

        internal void ConfigureEffects(FeedbackEffects feedbackEffects) => effects = feedbackEffects;

        internal void ConfigureCareer(CareerKeeper careerKeeper) => career = careerKeeper;

        [Tooltip("Optional: the counter can be moved (T034); the line then walks to its new place.")]
        [SerializeField] private PlacementKeeper counterPlacement;

        internal void ConfigureCounterPlacement(PlacementKeeper keeper) => counterPlacement = keeper;

        /// <summary>The counter moved: everyone in line walks to their place at it again.</summary>
        public void RepositionLine()
        {
            for (var i = 0; i < _line.Count; i++)
            {
                if (_line[i].Customer != null)
                    _line[i].Customer.GoTo(SlotPosition(i), customerServicePoint.rotation, settings.CustomerWalkSpeed);
            }
        }

        public FeedbackEffects Effects => effects;

        internal void Configure(ShiftSettings shiftSettings, HandScanner handScanner, HandoverZone zone,
            Customer prefab, Transform spawn, Transform service, CounterBoard counterBoard, ShiftSummaryPanel summary,
            GameObject restart, FeedbackAudio audio, SupplyHatch supplyHatch, DifficultyButton[] difficulty)
        {
            hatch = supplyHatch;
            difficultyButtons = difficulty ?? Array.Empty<DifficultyButton>();
            settings = shiftSettings;
            scanner = handScanner;
            handoverZone = zone;
            customerPrefab = prefab;
            customerSpawnPoint = spawn;
            customerServicePoint = service;
            board = counterBoard;
            summaryPanel = summary;
            restartButton = restart;
            feedback = audio;
        }
    }
}