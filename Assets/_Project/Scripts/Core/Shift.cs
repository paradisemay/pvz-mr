using System;
using System.Collections.Generic;

namespace Pvz.Core
{
    /// <summary>
    /// One work shift: the warehouse, the counter, order generation, the scheduled supply delivery and statistics.
    /// The shift is over when the delivery has arrived and every box has been received and handed over.
    /// </summary>
    public sealed class Shift : IDisposable
    {
        private readonly Dictionary<BoxId, BoxRecord> _pendingIntake = new Dictionary<BoxId, BoxRecord>();
        private readonly HashSet<string> _orderNumbers = new HashSet<string>();
        private readonly SupplyPlan _supplyPlan;
        private OrderGenerator _generator;

        public Shift(BoxCatalog catalog, IRandomSource random, ScoringRules rules = null, SupplyPlan supplyPlan = null,
            int bagChancePercent = 0)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            Desk = new PickupDesk(catalog);
            Stats = new ShiftStats(Desk, rules);
            BoxReceived += Stats.OnBoxReceived;
            _generator = new OrderGenerator(random ?? throw new ArgumentNullException(nameof(random)), bagChancePercent);
            _supplyPlan = supplyPlan ?? SupplyPlan.None;
            foreach (var box in catalog.Boxes) _orderNumbers.Add(box.OrderNumber);
            HasScheduledDelivery = !_supplyPlan.IsEmpty;
        }

        /// <summary>A supply box was scanned and moved into the warehouse.</summary>
        public event Action<BoxRecord> BoxReceived;

        /// <summary>A supply delivery has arrived (its boxes are not in the warehouse until scanned).</summary>
        public event Action<IReadOnlyList<BoxRecord>> DeliveryExpected;

        public PickupDesk Desk { get; }
        public ShiftStats Stats { get; }

        /// <summary>Number of different orders of the shift so far (initial stock + arrived deliveries).</summary>
        public int OrdersTotal => _orderNumbers.Count;

        public bool HasOrdersLeft => Desk.Catalog.Count > 0;

        public bool HasPendingIntake => _pendingIntake.Count > 0;

        public int PendingIntakeCount => _pendingIntake.Count;

        /// <summary>Size of the last delivery and how many of its boxes were already received.</summary>
        public int DeliverySize { get; private set; }
        public int DeliveryReceived => DeliverySize - _pendingIntake.Count;

        /// <summary>The planned delivery has not arrived yet.</summary>
        public bool HasScheduledDelivery { get; private set; }

        /// <summary>
        /// The planned delivery should arrive now: enough customers were served, or there is nothing left to sell.
        /// Never due while a customer is being served.
        /// </summary>
        public bool IsDeliveryDue => HasScheduledDelivery && !Desk.HasActiveOrder &&
                                     (Stats.OrdersCompleted >= _supplyPlan.AfterOrders || !HasOrdersLeft);

        public bool IsPendingIntake(BoxId id) => _pendingIntake.ContainsKey(id);

        public bool IsOver => !HasOrdersLeft && !Desk.HasActiveOrder && !HasPendingIntake && !HasScheduledDelivery;

        /// <summary>Replaces the randomness (e.g. a fixed seed in tests). Affects orders created afterwards.</summary>
        public void UseRandomSource(IRandomSource random)
        {
            _generator = new OrderGenerator(random ?? throw new ArgumentNullException(nameof(random)), BagChancePercent);
        }

        /// <summary>Share of the patience after which the customer starts hurrying the player.</summary>
        public const double HurryFraction = 0.6;

        /// <summary>The customer told the player to hurry up (once per customer).</summary>
        public event Action<Order> CustomerHurried;

        /// <summary>The customer ran out of patience and left; the order's boxes stay in the warehouse.</summary>
        public event Action<Order> OrderAbandoned
        {
            add => Desk.OrderAbandoned += value;
            remove => Desk.OrderAbandoned -= value;
        }

        /// <summary>How long a customer waits at the counter, seconds; 0 = forever.</summary>
        public double PatienceSeconds { get; set; }

        /// <summary>
        /// Advances customer patience. Call regularly with the current time.
        /// A customer who has already received part of the order waits until it is complete: leaving with some boxes
        /// would take them out of stock and leave an incomplete order behind.
        /// </summary>
        public void Tick(double now)
        {
            if (PatienceSeconds <= 0 || !Desk.HasActiveOrder) return;
            var session = Desk.ActiveSession;
            if (session.DeliveredCount > 0) return;
            var waited = now - session.OpenedAt;
            if (!session.Hurried && waited >= PatienceSeconds * HurryFraction)
            {
                session.MarkHurried();
                CustomerHurried?.Invoke(session.Order);
            }
            if (waited >= PatienceSeconds) Desk.Abandon();
        }

        /// <summary>Probability (0..100) that a customer also asks for a bag.</summary>
        public int BagChancePercent
        {
            get => _generator.BagChancePercent;
            set => _generator.BagChancePercent = value;
        }

        /// <summary>Picks the next customer's order, or null when the warehouse is empty.</summary>
        public Order NextOrder()
        {
            if (Desk.HasActiveOrder)
                throw new InvalidOperationException("The current customer has not been served yet.");
            return _generator.CreateRandomOrder(Desk.Catalog);
        }

        /// <summary>If the planned delivery is due, makes it arrive and returns its boxes (to be spawned in the room).</summary>
        public bool TryTakeDueDelivery(out IReadOnlyList<BoxRecord> boxes)
        {
            if (!IsDeliveryDue)
            {
                boxes = Array.Empty<BoxRecord>();
                return false;
            }
            HasScheduledDelivery = false;
            boxes = _supplyPlan.Boxes;
            ExpectDelivery(boxes);
            return true;
        }

        /// <summary>Announces an (unplanned) supply delivery. The boxes must be scanned to enter the warehouse.</summary>
        public void ExpectDelivery(IEnumerable<BoxRecord> boxes)
        {
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));
            var delivery = new List<BoxRecord>(boxes);
            // Validate the whole batch before changing anything, so a bad delivery never half-applies.
            var ids = new HashSet<BoxId>();
            foreach (var box in delivery)
            {
                if (box == null) throw new ArgumentException("Delivery contains a null box.", nameof(boxes));
                if (!ids.Add(box.Id) || Desk.Catalog.Contains(box.Id) || _pendingIntake.ContainsKey(box.Id))
                    throw new ArgumentException($"Box {box.Id} is duplicated, already in the warehouse or on its way.", nameof(boxes));
                // A delivery brings new orders only; topping up an existing order would let it be issued incomplete.
                if (_orderNumbers.Contains(box.OrderNumber))
                    throw new ArgumentException($"Order {box.OrderNumber} already exists in this shift.", nameof(boxes));
            }
            foreach (var box in delivery)
            {
                _pendingIntake.Add(box.Id, box);
                _orderNumbers.Add(box.OrderNumber);
            }
            DeliverySize = _pendingIntake.Count;
            DeliveryExpected?.Invoke(delivery);
        }

        /// <summary>
        /// Scanner input: accepts a delivered box into the warehouse on its first scan,
        /// otherwise asks the counter how the box relates to the current customer.
        /// </summary>
        public ScanOutcome Scan(BoxId id)
        {
            if (!_pendingIntake.TryGetValue(id, out var record)) return Desk.Scan(id);

            _pendingIntake.Remove(id);
            Desk.Catalog.Add(record);
            BoxReceived?.Invoke(record);
            return new ScanOutcome(ScanStatus.Received, id, record.OrderNumber);
        }

        /// <summary>
        /// Hands over boxes that were already waiting in the handover zone when the customer arrived.
        /// Only boxes the customer actually needs are offered, so waiting boxes never count as mistakes.
        /// </summary>
        public IReadOnlyList<HandoverOutcome> HandOverWaiting(IEnumerable<BoxId> boxesInZone, double now)
        {
            if (boxesInZone == null) throw new ArgumentNullException(nameof(boxesInZone));
            var outcomes = new List<HandoverOutcome>();
            foreach (var box in boxesInZone)
            {
                if (!Desk.HasActiveOrder) break;
                if (Desk.ActiveSession.Requires(box)) outcomes.Add(Desk.Handover(box, now));
            }
            return outcomes;
        }

        public void Dispose()
        {
            BoxReceived -= Stats.OnBoxReceived;
            Stats.Dispose();
        }
    }
}
