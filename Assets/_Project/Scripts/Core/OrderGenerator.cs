using System;
using System.Collections.Generic;

namespace Pvz.Core
{
    /// <summary>Creates customer orders from the boxes that are still in the catalog.</summary>
    public sealed class OrderGenerator
    {
        private readonly IRandomSource _random;

        public OrderGenerator(IRandomSource random, int bagChancePercent = 0)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
            BagChancePercent = bagChancePercent;
        }

        /// <summary>Probability (0..100) that a customer also wants a bag.</summary>
        public int BagChancePercent { get; set; }

        /// <summary>Probability (0..100) that a customer is in a hurry.</summary>
        public int UrgentChancePercent { get; set; }

        /// <summary>Probability (0..100) that a customer remembers only the end of the order number.</summary>
        public int ForgotNumberChancePercent { get; set; }

        /// <summary>
        /// Picks a random box and returns the order it belongs to (all boxes sharing that order number),
        /// or null if the catalog is empty.
        /// </summary>
        public Order CreateRandomOrder(BoxCatalog catalog) => CreateRandomOrder(catalog, null);

        /// <summary>Same, but never picks an order whose number is in <paramref name="excludedOrders"/>.</summary>
        public Order CreateRandomOrder(BoxCatalog catalog, ICollection<string> excludedOrders) =>
            CreateRandomOrder(catalog, excludedOrders, null);

        /// <summary>
        /// Same; <paramref name="otherKnownNumbers"/> are orders that are not in the warehouse yet (deliveries on their
        /// way) but would still make a half-remembered number ambiguous.
        /// </summary>
        public Order CreateRandomOrder(BoxCatalog catalog, ICollection<string> excludedOrders,
            IEnumerable<string> otherKnownNumbers)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            var candidates = new List<BoxRecord>();
            foreach (var box in catalog.Boxes)
            {
                if (excludedOrders == null || !excludedOrders.Contains(box.OrderNumber)) candidates.Add(box);
            }
            if (candidates.Count == 0) return null;

            var picked = candidates[_random.Range(0, candidates.Count)];
            var boxes = new List<BoxId>();
            foreach (var box in catalog.Boxes)
            {
                if (box.OrderNumber == picked.OrderNumber) boxes.Add(box.Id);
            }
            // Roll each trait only when it is possible, so shifts without it keep the same order sequence.
            var needsBag = Roll(BagChancePercent);
            var urgent = Roll(UrgentChancePercent);
            var forgot = Roll(ForgotNumberChancePercent) && HintIsUnambiguous(picked.OrderNumber, catalog, otherKnownNumbers);
            return new Order(picked.OrderNumber, boxes, needsBag: needsBag, isUrgent: urgent, forgotNumber: forgot);
        }

        private bool Roll(int chancePercent) => chancePercent > 0 && _random.Range(0, 100) < chancePercent;

        /// <summary>
        /// No other order of the shift (in the warehouse or on its way) ends with the same digits, so the hint names
        /// exactly one order.
        /// </summary>
        private static bool HintIsUnambiguous(string number, BoxCatalog catalog, IEnumerable<string> otherKnownNumbers)
        {
            if (!Order.CanHint(number)) return false;
            var hint = Order.HintOf(number);
            bool Clashes(string other) => other != number && other.EndsWith(hint, StringComparison.Ordinal);
            foreach (var box in catalog.Boxes)
            {
                if (Clashes(box.OrderNumber)) return false;
            }
            if (otherKnownNumbers == null) return true;
            foreach (var other in otherKnownNumbers)
            {
                if (other != null && Clashes(other)) return false;
            }
            return true;
        }
    }
}
