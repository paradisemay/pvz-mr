using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Pvz.Core.Tests
{
    /// <summary>T047 (#86): «потеряшки» — orders missing a box, reported with «Не найден».</summary>
    public class LostOrderTests
    {
        private static readonly BoxId A1 = new BoxId("BOX-301"), A2 = new BoxId("BOX-302"), B = new BoxId("BOX-303");

        private static Shift ShiftWith(params BoxId[] missing)
        {
            var shift = new Shift(new BoxCatalog(new[] { new BoxRecord(A1, "4401"), new BoxRecord(A2, "4401"), new BoxRecord(B, "5502") }),
                new SystemRandomSource(1)) { QueueLength = 1 };
            shift.MarkMissing(missing);
            return shift;
        }

        [Test]
        public void AMissingBox_ReportedNotFound_ClosesTheOrderAsLost_ForPoints()
        {
            var shift = ShiftWith(A2);
            var order = new Order("4401", new[] { A1, A2 });
            shift.Desk.OpenOrder(order, 0);
            shift.Desk.Handover(A1, 1); // the box that is there
            var before = shift.Stats.Score;
            Order lost = null;
            shift.OrderLost += o => lost = o;

            Assert.IsTrue(shift.DeclareNotFound());
            Assert.AreSame(order, lost);
            Assert.IsFalse(shift.Desk.HasCustomer, "the customer leaves calmly");
            Assert.AreEqual(1, shift.Stats.LostOrders);
            Assert.AreEqual(0, shift.Stats.OrdersAbandoned, "not an angry customer");
            Assert.AreEqual(before + ScoringRules.Default.LostOrderReported, shift.Stats.Score);
            Assert.IsFalse(shift.IsForSale("4401"), "nobody else is sold this order");
        }

        [Test]
        public void NotFound_WhileEveryBoxIsThere_IsAPenalty_AndTheCustomerStays()
        {
            var shift = ShiftWith(); // nothing missing
            shift.Desk.OpenOrder(new Order("5502", new[] { B }), 0);
            var before = shift.Stats.Score;
            Assert.IsFalse(shift.DeclareNotFound());
            Assert.IsTrue(shift.Desk.HasActiveOrder, "the box is there: the customer keeps waiting");
            Assert.AreEqual(1, shift.Stats.FalseNotFound);
            Assert.AreEqual(before - ScoringRules.Default.FalseNotFoundPenalty, shift.Stats.Score);
        }

        [Test]
        public void AMissingBoxAlreadyHandedOver_IsNotMissing_ANotFoundIsWrong()
        {
            var shift = ShiftWith(); // nothing is missing at all
            shift.Desk.OpenOrder(new Order("4401", new[] { A1, A2 }), 0);
            shift.Desk.Handover(A1, 1);
            Assert.IsFalse(shift.DeclareNotFound(), "A2 is in the warehouse");
        }

        [Test]
        public void TheLostOrder_IsMarkedInTheBook()
        {
            var shift = ShiftWith(B);
            Assert.IsTrue(shift.TryEnqueue(out var visit));
            while (visit.Order.Number != "5502")
            {
                shift.LeaveQueue(visit);
                Assert.IsTrue(shift.TryEnqueue(out visit));
            }
            shift.OpenNext(0);
            Assert.IsTrue(shift.DeclareNotFound());
            Assert.AreEqual(VisitState.Lost, shift.Book.Entries.Last(e => e.Visit == visit).State);
        }

        [Test]
        public void PickMissing_TakesOneBoxOfSomeOrders_NeverASpare_SameForASeed()
        {
            var stock = new List<BoxRecord>
            {
                new BoxRecord(A1, "4401"), new BoxRecord(A2, "4401"), new BoxRecord(B, "5502"),
                new BoxRecord(new BoxId("BOX-201"), "8804"),
            };
            var spares = new HashSet<string> { "8804" };
            var all = StockPlan.PickMissing(stock, spares, 100, new SystemRandomSource(3));
            Assert.AreEqual(2, all.Count, "one box from each of the two orders for sale");
            Assert.AreEqual(1, all.Count(id => id == A1 || id == A2), "a multi-box order misses only one box");
            CollectionAssert.DoesNotContain(all, new BoxId("BOX-201"));
            Assert.IsEmpty(StockPlan.PickMissing(stock, spares, 0, new SystemRandomSource(3)));
            CollectionAssert.AreEqual(all, StockPlan.PickMissing(stock, spares, 100, new SystemRandomSource(3)));
        }
    }
}
