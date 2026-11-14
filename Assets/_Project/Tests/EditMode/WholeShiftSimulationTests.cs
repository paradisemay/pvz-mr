using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Pvz.Core.Tests
{
    /// <summary>
    /// A whole shift played by the rules, many seeds: a long stock (T044), returns and refusals (T045). No order is
    /// sold twice and the served count never exceeds the orders of the shift.
    /// </summary>
    public class WholeShiftSimulationTests
    {
        [Test]
        public void EveryOrder_IsSoldOnce_WithReturnsAndRefusals([Range(1, 40)] int seed)
        {
            var stock = new List<BoxRecord>
            {
                new BoxRecord(new BoxId("BOX-001"), "1435"),
                new BoxRecord(new BoxId("BOX-002"), "2781"),
                new BoxRecord(new BoxId("BOX-003"), "5812"),
            };
            stock.AddRange(StockPlan.Extra(stock, null, null, 14, new SystemRandomSource(seed)));
            var shift = new Shift(new BoxCatalog(stock), new SystemRandomSource(seed * 7 + 1))
            {
                QueueLength = 4, ReturnChancePercent = 35, RefusalChancePercent = 25,
            };
            var sold = new List<string>();
            shift.Desk.OrderCompleted += r => sold.Add(r.Order.Number);

            var now = 0.0;
            for (var step = 0; step < 500 && shift.HasOrdersLeft; step++)
            {
                while (shift.TryEnqueue(out _)) { }
                if (shift.QueuedVisits.Count == 0) break;
                var visit = shift.OpenNext(now += 10);
                if (visit.IsReturn) shift.Desk.Scan(visit.Return.Box);
                else
                {
                    foreach (var box in visit.Order.Boxes) shift.Desk.Handover(box, now += 1);
                    if (shift.Desk.HasActiveOrder && shift.Desk.ActiveSession.AwaitingBag) shift.Desk.HandOverBag(now += 1);
                }
                Assert.IsFalse(shift.Desk.HasCustomer, $"step {step}: the customer was served");
            }

            Assert.IsFalse(shift.HasOrdersLeft, "the shift sold everything");
            var twice = sold.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
            CollectionAssert.IsEmpty(twice, "an order sold twice");
            Assert.LessOrEqual(shift.Stats.OrdersCompleted, shift.OrdersTotal);
            Assert.AreEqual(shift.OrdersTotal, shift.Stats.OrdersCompleted);
        }
    }
}
