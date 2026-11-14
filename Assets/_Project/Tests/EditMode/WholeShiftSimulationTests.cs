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
        private static Shift Play(int seed, out List<string> sold)
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
            var list = new List<string>();
            shift.Desk.OrderCompleted += r => list.Add(r.Order.Number);

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
            sold = list;
            return shift;
        }

        [Test]
        public void EveryOrder_IsSoldOnce_WithReturnsAndRefusals([Range(1, 40)] int seed)
        {
            var shift = Play(seed, out var sold);
            Assert.IsFalse(shift.HasOrdersLeft, "the shift sold everything");
            var twice = sold.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
            CollectionAssert.IsEmpty(twice, "an order sold twice");
            Assert.AreEqual(shift.OrdersTotal, shift.Stats.OrdersCompleted);
            Assert.AreEqual(shift.Stats.Refusals + shift.Stats.ReturnsAccepted, shift.Returns.Pending,
                "every refusal and return waits for the bin");
        }

        [Test]
        public void TheSimulatedShifts_HaveRefusalsAndReturns()
        {
            int refusals = 0, returns = 0;
            for (var seed = 1; seed <= 40; seed++)
            {
                var shift = Play(seed, out _);
                refusals += shift.Stats.Refusals;
                returns += shift.Stats.ReturnsAccepted;
            }
            Assert.Greater(refusals, 40, "about a quarter of 14 orders, 40 times");
            Assert.Greater(returns, 0);
        }
    }
}
