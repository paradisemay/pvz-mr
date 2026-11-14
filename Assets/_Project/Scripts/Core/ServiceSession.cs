using System;
using System.Collections.Generic;

namespace Pvz.Core
{
    /// <summary>
    /// Serving one customer: which order they want, when service started, what was already handed over
    /// and how many wrong boxes were offered. Owned and mutated only by <see cref="PickupDesk"/>.
    /// </summary>
    public sealed class ServiceSession
    {
        private readonly HashSet<BoxId> _delivered = new HashSet<BoxId>();
        private readonly HashSet<BoxId> _rejected = new HashSet<BoxId>();

        internal ServiceSession(Order order, double openedAt)
        {
            Order = order ?? throw new ArgumentNullException(nameof(order));
            OpenedAt = openedAt;
        }

        public Order Order { get; }
        public double OpenedAt { get; }
        public int Mistakes { get; private set; }
        public int DeliveredCount => _delivered.Count;

        private readonly HashSet<BoxId> _scanned = new HashSet<BoxId>();

        /// <summary>
        /// T043 (#83): items of the order the scanner found — or handed over without a scan: the count only goes up as
        /// the order gets done («Товаров 8, отсканировано 7»).
        /// </summary>
        public int ScannedCount
        {
            get
            {
                var count = _scanned.Count;
                foreach (var id in _delivered) if (!_scanned.Contains(id)) count++;
                return count;
            }
        }

        internal void MarkScanned(BoxId id)
        {
            if (Order.Contains(id)) _scanned.Add(id);
        }
        public int RemainingCount => Order.Boxes.Count - _delivered.Count;
        public bool BagDelivered { get; private set; }

        /// <summary>All boxes are handed over and the customer now asks for a bag.</summary>
        public bool AwaitingBag => RemainingCount == 0 && Order.NeedsBag && !BagDelivered;

        public bool IsComplete => RemainingCount == 0 && (!Order.NeedsBag || BagDelivered);

        /// <summary>Records the bag if the customer is asking for it now (all boxes handed over).</summary>
        internal BagStatus DeliverBag()
        {
            if (!Order.NeedsBag || BagDelivered) return BagStatus.NotNeeded;
            if (RemainingCount > 0) return BagStatus.TooEarly;
            BagDelivered = true;
            return BagStatus.Accepted;
        }

        /// <summary>The customer already told the player to hurry up.</summary>
        public bool Hurried { get; private set; }

        internal void MarkHurried() => Hurried = true;

        public bool Requires(BoxId id) => Order.Contains(id) && !_delivered.Contains(id);

        public bool IsDelivered(BoxId id) => _delivered.Contains(id);

        internal void MarkDelivered(BoxId id)
        {
            if (!Requires(id)) throw new InvalidOperationException($"Box {id} is not pending in order {Order.Number}.");
            _delivered.Add(id);
        }

        /// <summary>Records a wrong box; returns false if this box was already rejected by this customer.</summary>
        internal bool Reject(BoxId id)
        {
            if (!_rejected.Add(id)) return false;
            Mistakes++;
            return true;
        }
    }
}
