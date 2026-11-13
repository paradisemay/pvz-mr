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

        /// <summary>
        /// Picks a random box and returns the order it belongs to (all boxes sharing that order number),
        /// or null if the catalog is empty.
        /// </summary>
        public Order CreateRandomOrder(BoxCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (catalog.Count == 0) return null;

            var picked = catalog.Boxes[_random.Range(0, catalog.Count)];
            var boxes = new List<BoxId>();
            foreach (var box in catalog.Boxes)
            {
                if (box.OrderNumber == picked.OrderNumber) boxes.Add(box.Id);
            }
            // Roll only when bags are possible, so shifts without bags keep the same order sequence.
            var needsBag = BagChancePercent > 0 && _random.Range(0, 100) < BagChancePercent;
            return new Order(picked.OrderNumber, boxes, needsBag);
        }
    }
}
