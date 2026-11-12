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

        private readonly BoxRegistry _registry = new BoxRegistry();
        private ShiftPresenter _presenter;
        private int _customersSpawned;

        public Shift Shift { get; private set; }
        public PickupDesk Desk => Shift.Desk;
        public ShiftStats Stats => Shift.Stats;
        public int OrdersTotal => Shift.OrdersTotal;
        public Customer CurrentCustomer { get; private set; }
        public bool IsShiftOver { get; private set; }
        public HandScanner Scanner => scanner;
        public HandoverZone Zone => handoverZone;
        public IEnumerable<QrBox> RemainingBoxes => _registry.All;

        public event Action ShiftEnded;

        private static double Now => Time.timeAsDouble;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

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

            Shift = new Shift(catalog, settings.CreateRandomSource(), settings.CreateScoringRules());
            _presenter = new ShiftPresenter(Shift, board, scanner, feedback);
        }

        private void OnEnable()
        {
            if (Shift == null) return;
            scanner.BoxScanned += OnBoxScanned;
            scanner.Missed += OnScanMissed;
            handoverZone.BoxEntered += OnBoxEnteredZone;
        }

        private void OnDisable()
        {
            if (Shift == null) return;
            scanner.BoxScanned -= OnBoxScanned;
            scanner.Missed -= OnScanMissed;
            handoverZone.BoxEntered -= OnBoxEnteredZone;
        }

        private void OnDestroy()
        {
            _presenter?.Dispose();
            Shift?.Dispose();
        }

        private void Start()
        {
            summaryPanel.Hide();
            restartButton.SetActive(false);
            _presenter.ShowShiftStarting();
            StartCoroutine(RunShift());
        }

        /// <summary>Fixes the order sequence (tests). Call before the first customer arrives.</summary>
        internal void UseRandomSeed(int seed) => Shift.UseRandomSource(new SystemRandomSource(seed));

        public bool TryGetBox(BoxId id, out QrBox box) => _registry.TryGet(id, out box);

        private IEnumerator RunShift()
        {
            yield return new WaitForSeconds(settings.FirstCustomerDelay);
            while (Shift.HasOrdersLeft)
            {
                yield return ServeCustomer(Shift.NextOrder());
                if (!Shift.HasOrdersLeft) break;
                _presenter.ShowWaitingForCustomer();
                yield return new WaitForSeconds(settings.DelayBetweenCustomers);
            }
            EndShift();
        }

        private IEnumerator ServeCustomer(Order order)
        {
            CurrentCustomer = Instantiate(customerPrefab, customerSpawnPoint.position, customerSpawnPoint.rotation);
            CurrentCustomer.SetLook(_customersSpawned++);
            _presenter.Customer = CurrentCustomer;
            _presenter.ShowCustomerApproaching();
            yield return CurrentCustomer.WalkTo(customerServicePoint.position, customerServicePoint.rotation, settings.CustomerWalkSpeed);

            Desk.OpenOrder(order, Now);
            HandOverBoxesAlreadyInZone();

            yield return new WaitUntil(() => !Desk.HasActiveOrder);
            yield return new WaitForSeconds(settings.ThanksDuration);

            CurrentCustomer.StopTalking();
            yield return CurrentCustomer.WalkTo(customerSpawnPoint.position, customerSpawnPoint.rotation, settings.CustomerWalkSpeed);
            _presenter.Customer = null;
            Destroy(CurrentCustomer.gameObject); // takes the handed-over boxes with it
            CurrentCustomer = null;
        }

        private void EndShift()
        {
            IsShiftOver = true;
            _presenter.ShowShiftOver();
            summaryPanel.Show(Stats, OrdersTotal);
            restartButton.SetActive(true);
            ShiftEnded?.Invoke();
        }

        private void OnBoxScanned(QrBox box)
        {
            if (box != null) Desk.Scan(box.Id);
        }

        private void OnScanMissed() => _presenter.ShowScanMissed();

        private void OnBoxEnteredZone(QrBox box)
        {
            var outcome = Desk.Handover(box.Id, Now);
            if (outcome.Status == HandoverStatus.Accepted) GiveToCustomer(box);
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

        internal void Configure(ShiftSettings shiftSettings, QrBox[] sceneBoxes, HandScanner handScanner, HandoverZone zone,
            Customer prefab, Transform spawn, Transform service, CounterBoard counterBoard, ShiftSummaryPanel summary,
            GameObject restart, FeedbackAudio audio)
        {
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
