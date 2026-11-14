using System;
using System.Collections.Generic;

namespace Pvz.Core
{
    /// <summary>
    /// T044 (#84): a shift long enough to play — Лёгкая ≈ 6 customers, Обычная ≈ 10, Сложная ≈ 14. The hand-made
    /// boxes of the difficulty preset (the look-alike pair, the big order, the supply) stay; the stock is filled up with
    /// generated orders of one or two boxes until the shift has <c>targetOrders</c> orders in all.
    /// Deterministic for a seed: the same shift has the same stock.
    /// </summary>
    public static class StockPlan
    {
        public const string GeneratedPrefix = "BOX-3";

        /// <param name="stock">Boxes that start the shift in the warehouse (spares included: their orders are not sold).</param>
        /// <param name="later">Boxes that come later (the supply): their orders count, their numbers are taken.</param>
        /// <param name="notForSale">Orders nobody comes for (the spare boxes).</param>
        /// <param name="twoBoxPercent">Chance that a generated order has two boxes.</param>
        public static List<BoxRecord> Extra(IEnumerable<BoxRecord> stock, IEnumerable<BoxRecord> later, ICollection<string> notForSale,
            int targetOrders, IRandomSource random, int twoBoxPercent = 25)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var takenNumbers = new HashSet<string>();
            var takenIds = new HashSet<string>();
            var sold = new HashSet<string>();
            foreach (var box in Concat(stock, later))
            {
                takenNumbers.Add(box.OrderNumber);
                takenIds.Add(box.Id.Value);
                if (notForSale == null || !notForSale.Contains(box.OrderNumber)) sold.Add(box.OrderNumber);
            }

            var extra = new List<BoxRecord>();
            var nextId = 1;
            for (var orders = sold.Count; orders < targetOrders; orders++)
            {
                var number = NewNumber(takenNumbers, random);
                var boxes = random.Range(0, 100) < twoBoxPercent ? 2 : 1;
                for (var i = 0; i < boxes; i++)
                {
                    string id;
                    do id = $"{GeneratedPrefix}{nextId++:00}"; while (takenIds.Contains(id));
                    takenIds.Add(id);
                    extra.Add(new BoxRecord(new BoxId(id), number));
                }
            }
            return extra;
        }

        /// <summary>A four-digit order number not used yet and not a look-alike of one (traps are hand-made, not random).</summary>
        private static string NewNumber(HashSet<string> taken, IRandomSource random)
        {
            for (var attempt = 0; attempt < 1000; attempt++)
            {
                var number = random.Range(1000, 10000).ToString();
                if (taken.Contains(number)) continue;
                var lookAlike = false;
                foreach (var other in taken) if (OrderNumbers.LookAlike(number, other)) { lookAlike = true; break; }
                if (lookAlike) continue;
                taken.Add(number);
                return number;
            }
            throw new InvalidOperationException("No free order number left.");
        }

        private static IEnumerable<BoxRecord> Concat(IEnumerable<BoxRecord> a, IEnumerable<BoxRecord> b)
        {
            if (a != null) foreach (var x in a) yield return x;
            if (b != null) foreach (var x in b) yield return x;
        }
    }
}
