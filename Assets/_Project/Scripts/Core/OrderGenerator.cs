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
        public Order CreateRandomOrder(BoxCatalog catalog, ICollection<string> excludedOrders)
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
            var forgot = Roll(ForgotNumberChancePercent) && HintIsUnambiguous(catalog, picked.OrderNumber);
            return new Order(picked.OrderNumber, boxes, needsBag, urgent, forgot);
        }

        private bool Roll(int chancePercent) => chancePercent > 0 && _random.Range(0, 100) < chancePercent;

        /// <summary>No other order in the warehouse ends with the same digits, so the hint names exactly one order.</summary>
        private static bool HintIsUnambiguous(BoxCatalog catalog, string number)
        {
            if (!Order.CanHint(number)) return false;
            var hint = Order.HintOf(number);
            foreach (var box in catalog.Boxes)
            {
                if (box.OrderNumber != number && box.OrderNumber.EndsWith(hint, StringComparison.Ordinal)) return false;
            }
            return true;
        }
    }
}
