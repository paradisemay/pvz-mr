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
        [SerializeField] private QrBox[] boxes = Array.Empty<QrBox>();
        [SerializeField] private HandScanner scanner;
        [SerializeField] private HandoverZone handoverZone;
        [SerializeField] private Customer customerPrefab;
        [SerializeField] private Transform customerSpawnPoint;
        [SerializeField] private Transform customerServicePoint;
        [SerializeField] private CounterBoard board;
        [SerializeField] private ShiftSummaryPanel summaryPanel;
        [SerializeField] private GameObject restartButton;
        [SerializeField] private FeedbackAudio feedback;
        [SerializeField] private SupplyDrop supplyDrop;
        [Tooltip("Optional: buttons to pick the difficulty. Without them the shift starts at once with the default settings.")]
        [SerializeField] private DifficultyButton[] difficultyButtons = Array.Empty<DifficultyButton>();
        [Tooltip("Optional: everything shown before the shift (difficulty buttons, recenter button); hidden on start.")]
        [SerializeField] private GameObject startMenu;
        [Tooltip("Generic box prefab carried by customers who come to return something.")]
        [SerializeField] private QrBox returnBoxPrefab;

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

        /// <summary>Sets how often customers are in a hurry or forget the order number (tests).</summary>
        internal void UseEventChances(int urgentPercent, int forgotNumberPercent)
        {
            Shift.UrgentChancePercent = urgentPercent;
            Shift.ForgotNumberChancePercent = forgotNumberPercent;
        }

        public IReadOnlyList<DifficultyButton> DifficultyButtons => difficultyButtons;

        public event Action ShiftEnded;

        private static double Now => Time.timeAsDouble;

        private void Awake()
        {
            if (!ValidateReferences()) enabled = false;
            _bestScores = new BestScores(new PlayerPrefsScoreStore());
        }

        private void OnEnable()
        {
            foreach (var button in difficultyButtons) if (button != null) button.Chosen += Begin;
            if (Shift != null) Subscribe();
        }

        private void OnDisable()
        {
            foreach (var button in difficultyButtons) if (button != null) button.Chosen -= Begin;
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
            if (HasDifficultyMenu) ShiftPresenter.ShowDifficultyMenu(board, MenuRecords());
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

            var catalog = new BoxCatalog();
            foreach (var box in boxes)
            {
                if (box == null) continue;
                if (!settings.TryGetOrderNumber(box.Id, out var orderNumber))
                {
                    Debug.LogWarning($"[ShiftDirector] Box {box.RawId} has no order in {settings.name}; it is ignored.", box);
                    continue;
                }
                if (!_registry.Register(box))
                {
                    Debug.LogWarning($"[ShiftDirector] Duplicate box {box.RawId}; it is ignored.", box);
                    continue;
                }
                catalog.Add(new BoxRecord(box.Id, orderNumber));
            }

            Shift = new Shift(catalog, settings.CreateRandomSource(), settings.CreateScoringRules(), settings.CreateSupplyPlan(),
                settings.BagChancePercent) {
                PatienceSeconds = settings.PatienceSeconds,
                QueueLength = settings.QueueLength,
                ReturnChancePercent = settings.ReturnChancePercent,
                UrgentChancePercent = settings.UrgentChancePercent,
                UrgentPatienceFactor = settings.UrgentPatienceFactor,
                ForgotNumberChancePercent = settings.ForgotNumberChancePercent,
                TimeLimitSeconds = settings.ShiftDurationSeconds,
            };
            Shift.Start(Now);
            if (Shift.ReturnChancePercent > 0 && returnBoxPrefab == null)
            {
                Debug.LogError("[ShiftDirector] Returns are enabled but there is no return box prefab; returns are turned off. Rebuild the scene.", this);
                Shift.ReturnChancePercent = 0;
            }
            _presenter = new ShiftPresenter(Shift, board, scanner, feedback, settings.ShowTutorial);
            _supplySeed = settings.CreateRandomSource().Range(1, int.MaxValue);
            Subscribe();

            foreach (var button in difficultyButtons) if (button != null) button.gameObject.SetActive(false);
            if (startMenu != null) startMenu.SetActive(false);
            _presenter.ShowShiftStarting(settings.DisplayName);
            StartCoroutine(RunShift());
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            scanner.BoxScanned += OnBoxScanned;
            scanner.Missed += OnScanMissed;
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
            if (supplyDrop == null)
            {
                Debug.LogError("[ShiftDirector] A delivery arrived but there is no SupplyDrop in the scene.", this);
                return;
            }
            var ids = delivery.Select(b => b.Id.Value).ToArray();
            foreach (var box in supplyDrop.Drop(ids, _supplySeed, settings.DefaultCeilingHeight)) _registry.Register(box);
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
            // Boxes the player prepared before the customer arrived are handed over at once (outside the
            // OrderOpened event, so Core never re-enters itself).
            if (Desk.HasActiveOrder) HandOverBoxesAlreadyInZone();

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
            try { record = _bestScores.Submit(settings.DifficultyId, Stats.Score); }
            catch (Exception e) { Debug.LogException(e, this); } // never leave the player without the summary and restart
            summaryPanel.Show(Stats, OrdersTotal, record, settings.DisplayName, Shift.MaxQueueLength, Shift.IsTimeUp);
            restartButton.SetActive(true);
            ShiftEnded?.Invoke();
        }

        private void OnBoxScanned(QrBox box)
        {
            if (box != null && !IsShiftOver) Shift.Scan(box.Id); // after the summary nothing scores any more
        }

        private void OnScanMissed() => _presenter.ShowScanMissed();

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

        internal void Configure(ShiftSettings shiftSettings, QrBox[] sceneBoxes, HandScanner handScanner, HandoverZone zone,
            Customer prefab, Transform spawn, Transform service, CounterBoard counterBoard, ShiftSummaryPanel summary,
            GameObject restart, FeedbackAudio audio, SupplyDrop supply, DifficultyButton[] difficulty)
        {
            supplyDrop = supply;
            difficultyButtons = difficulty ?? Array.Empty<DifficultyButton>();
            settings = shiftSettings;
            boxes = sceneBoxes;
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