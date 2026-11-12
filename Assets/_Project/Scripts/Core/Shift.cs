using System;
using System.Collections.Generic;

namespace Pvz.Core
{
    /// <summary>
    /// One work shift: the warehouse, the counter, order generation and statistics.
    /// The shift is over when every box has been handed over.
    /// </summary>
    public sealed class Shift : IDisposable
    {
        private OrderGenerator _generator;

        public Shift(BoxCatalog catalog, IRandomSource random, ScoringRules rules = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            Desk = new PickupDesk(catalog);
            Stats = new ShiftStats(Desk, rules);
            _generator = new OrderGenerator(random ?? throw new ArgumentNullException(nameof(random)));
            OrdersTotal = CountDistinctOrders(catalog);
        }

        public PickupDesk Desk { get; }
        public ShiftStats Stats { get; }

        /// <summary>Number of different orders in the warehouse when the shift started.</summary>
        public int OrdersTotal { get; }

        public bool HasOrdersLeft => Desk.Catalog.Count > 0;

        public bool IsOver => !HasOrdersLeft && !Desk.HasActiveOrder;

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

        private static int CountDistinctOrders(BoxCatalog catalog)
        {
            var numbers = new HashSet<string>();
            foreach (var box in catalog.Boxes) numbers.Add(box.OrderNumber);
            return numbers.Count;
        }
    }
}
