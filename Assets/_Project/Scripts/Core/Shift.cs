using System;
using System.Collections.Generic;

namespace Pvz.Core
{
    /// <summary>
    /// One work shift: the warehouse, the counter, order generation, supply deliveries and statistics.
    /// The shift is over when every box — including delivered ones — has been received and handed over.
    /// </summary>
    public sealed class Shift : IDisposable
    {
        private readonly Dictionary<BoxId, BoxRecord> _pendingIntake = new Dictionary<BoxId, BoxRecord>();
        private readonly HashSet<string> _orderNumbers = new HashSet<string>();
        private OrderGenerator _generator;

        public Shift(BoxCatalog catalog, IRandomSource random, ScoringRules rules = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            Desk = new PickupDesk(catalog);
            Stats = new ShiftStats(Desk, rules);
            _generator = new OrderGenerator(random ?? throw new ArgumentNullException(nameof(random)));
            foreach (var box in catalog.Boxes) _orderNumbers.Add(box.OrderNumber);
        }

        /// <summary>A supply box was scanned and moved into the warehouse.</summary>
        public event Action<BoxRecord> BoxReceived;

        /// <summary>A supply delivery is on its way (boxes are not in the warehouse until scanned).</summary>
        public event Action<IReadOnlyList<BoxRecord>> DeliveryExpected;

        public PickupDesk Desk { get; }
        public ShiftStats Stats { get; }

        /// <summary>Number of different orders of the shift so far (initial stock + announced deliveries).</summary>
        public int OrdersTotal => _orderNumbers.Count;

        public bool HasOrdersLeft => Desk.Catalog.Count > 0;

        public bool HasPendingIntake => _pendingIntake.Count > 0;

        public int PendingIntakeCount => _pendingIntake.Count;

        public bool IsPendingIntake(BoxId id) => _pendingIntake.ContainsKey(id);

        public bool IsOver => !HasOrdersLeft && !Desk.HasActiveOrder && !HasPendingIntake;

        /// <summary>Replaces the randomness (e.g. a fixed seed in tests). Affects orders created afterwards.</summary>
        public void UseRandomSource(IRandomSource random)
        {
            _generator = new OrderGenerator(random ?? throw new ArgumentNullException(nameof(random)));
        }

        /// <summary>Picks the next customer's order, or null when the warehouse is empty.</summary>
        public Order NextOrder()
        {
            if (Desk.HasActiveOrder)
                throw new InvalidOperationException("The current customer has not been served yet.");
            return _generator.CreateRandomOrder(Desk.Catalog);
        }

        /// <summary>Announces a supply delivery. The boxes must be scanned to enter the warehouse.</summary>
        public void ExpectDelivery(IEnumerable<BoxRecord> boxes)
        {
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));
            var delivery = new List<BoxRecord>(boxes);
            foreach (var box in delivery)
            {
                if (box == null) throw new ArgumentException("Delivery contains a null box.", nameof(boxes));
                if (Desk.Catalog.Contains(box.Id) || _pendingIntake.ContainsKey(box.Id))
                    throw new ArgumentException($"Box {box.Id} is already in the warehouse or on its way.", nameof(boxes));
            }
            foreach (var box in delivery)
            {
                _pendingIntake.Add(box.Id, box);
                _orderNumbers.Add(box.OrderNumber);
            }
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
            Stats.RecordIntake();
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

        public void Dispose() => Stats.Dispose();
    }
}
