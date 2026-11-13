using System;
using NUnit.Framework;

namespace Pvz.Core.Tests
{
    public class ReturnTests
    {
        /// <summary>Serves one pickup through the line so that its box becomes returnable.</summary>
        private static Order ServeFirstOrder(Shift shift, double now)
        {
            shift.ReturnChancePercent = 0;
            Assert.IsTrue(shift.TryEnqueue(out var visit));
            shift.OpenNext(now);
            shift.Desk.Handover(visit.Order.Boxes[0], now + 1);
            return visit.Order;
        }

        private static Shift NewShift() =>
            new Shift(TestData.MvpCatalog(), new SystemRandomSource(4)) { QueueLength = 2 };

        [Test]
        public void NoReturns_BeforeAnythingWasHandedOver()
        {
            using (var shift = NewShift())
            {
                shift.ReturnChancePercent = 100;
                Assert.IsTrue(shift.TryEnqueue(out var visit));
                Assert.IsFalse(visit.IsReturn, "nothing was sold yet, so nothing can come back");
            }
        }

        [Test]
        public void HandedOverBox_CanComeBack_AsAReturnVisit()
        {
            using (var shift = NewShift())
            {
                var sold = ServeFirstOrder(shift, 0);
                shift.ReturnChancePercent = 100;

                Assert.IsTrue(shift.TryEnqueue(out var visit));
                Assert.IsTrue(visit.IsReturn);
                Assert.AreEqual(sold.Boxes[0], visit.Return.Box);
                Assert.AreEqual(sold.Number, visit.Return.OrderNumber);
                Assert.IsNull(visit.Order);
            }
        }

        [Test]
        public void ScanningTheReturnedBox_AcceptsTheReturn_AndScores()
        {
            using (var shift = NewShift())
            {
                ServeFirstOrder(shift, 0);
                var scoreBefore = shift.Stats.Score;
                shift.ReturnChancePercent = 100;
                shift.TryEnqueue(out var visit);
                ReturnRequest accepted = null;
                shift.Desk.ReturnAccepted += r => accepted = r;

                Assert.AreSame(visit, shift.OpenNext(10));
                Assert.IsTrue(shift.Desk.HasActiveReturn);
                Assert.IsTrue(shift.Desk.HasCustomer);
                Assert.IsFalse(shift.Desk.HasActiveOrder);

                var outcome = shift.Scan(visit.Return.Box);

                Assert.AreEqual(ScanStatus.ReturnAccepted, outcome.Status);
                Assert.AreSame(visit.Return, accepted);
                Assert.IsFalse(shift.Desk.HasCustomer);
                Assert.AreEqual(1, shift.Stats.ReturnsAccepted);
                Assert.AreEqual(scoreBefore + 30, shift.Stats.Score);
                Assert.AreEqual(0, shift.QueuedVisits.Count, "the returning customer left the line");
            }
        }

        [Test]
        public void ScanningAnotherBox_DuringAReturn_DoesNotAcceptIt()
        {
            using (var shift = NewShift())
            {
                ServeFirstOrder(shift, 0);
                shift.ReturnChancePercent = 100;
                shift.TryEnqueue(out var visit);
                shift.OpenNext(10);

                var other = TestData.MvpCatalog().Boxes[0].Id == visit.Return.Box ? TestData.Box2 : TestData.Box1;
                Assert.AreNotEqual(ScanStatus.ReturnAccepted, shift.Scan(other).Status);
                Assert.IsTrue(shift.Desk.HasActiveReturn);
            }
        }

        [Test]
        public void NoOrderCanOpen_WhileAReturnIsProcessed()
        {
            using (var shift = NewShift())
            {
                ServeFirstOrder(shift, 0);
                shift.ReturnChancePercent = 100;
                shift.TryEnqueue(out _);
                shift.OpenNext(10);
                var order = shift.NextOrder();
                Assert.Throws<InvalidOperationException>(() => shift.Desk.OpenOrder(order, 11));
            }
        }

        [Test]
        public void EachBox_ComesBackAtMostOnce()
        {
            using (var shift = new Shift(TestData.MvpCatalog(), new SystemRandomSource(1)) { QueueLength = 3 })
            {
                ServeFirstOrder(shift, 0);
                shift.ReturnChancePercent = 100;
                shift.TryEnqueue(out var first);
                shift.TryEnqueue(out var second);
                Assert.IsTrue(first.IsReturn);
                Assert.IsFalse(second.IsReturn, "the only sold box is already on its way back");
            }
        }

        [Test]
        public void ShiftIsNotOver_WhileAReturnIsQueued()
        {
            using (var shift = new Shift(new BoxCatalog(new[]
                   {
                       new BoxRecord(TestData.Box1, "1435"),
                       new BoxRecord(TestData.Box2, "2781"),
                   }), new SystemRandomSource(2)) { QueueLength = 2 })
            {
                ServeFirstOrder(shift, 0);
                shift.ReturnChancePercent = 100;
                shift.TryEnqueue(out var returning);
                Assert.IsTrue(returning.IsReturn);

                // Sell the last order directly: the warehouse is empty, but the returning customer still waits.
                var last = shift.NextOrder();
                shift.Desk.OpenOrder(last, 5);
                shift.Desk.Handover(last.Boxes[0], 6);
                Assert.IsFalse(shift.HasOrdersLeft);
                Assert.IsFalse(shift.IsOver, "a returning customer is still in line");

                Assert.AreSame(returning, shift.OpenNext(7));
                shift.Scan(returning.Return.Box);
                Assert.IsTrue(shift.IsOver);
            }
        }
    }
}
