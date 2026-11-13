using System.Collections.Generic;
using NUnit.Framework;

namespace Pvz.Core.Tests
{
    /// <summary>T015: the shift lasts a limited time.</summary>
    public class ShiftTimerTests
    {
        private static Shift TimedShift(double seconds) =>
            new Shift(TestData.MvpCatalog(), new SystemRandomSource(7)) { QueueLength = 3, TimeLimitSeconds = seconds };

        [Test]
        public void WithoutALimit_TheClockNeverRunsOut()
        {
            using (var shift = TimedShift(0))
            {
                shift.Start(0);
                Assert.IsNull(shift.TimeLeft(0));
                shift.Tick(100000);
                Assert.IsFalse(shift.IsTimeUp);
            }
        }

        [Test]
        public void TimeLeft_CountsDown_OnlyAfterStart()
        {
            using (var shift = TimedShift(300))
            {
                Assert.IsNull(shift.TimeLeft(5), "the clock has not started");
                shift.Start(10);
                shift.Start(50); // only the first start counts
                Assert.AreEqual(300, shift.TimeLeft(10).Value, 1e-9);
                Assert.AreEqual(100, shift.TimeLeft(210).Value, 1e-9);
                Assert.AreEqual(0, shift.TimeLeft(999).Value, 1e-9);
            }
        }

        [Test]
        public void WhenTimeRunsOut_TheLineGoesHome_ButTheCustomerAtTheCounterIsServed()
        {
            using (var shift = TimedShift(60))
            {
                shift.Start(0);
                Assert.IsTrue(shift.TryEnqueue(out var atCounter));
                Assert.IsTrue(shift.TryEnqueue(out var second));
                Assert.IsTrue(shift.TryEnqueue(out var third));
                shift.OpenNext(1);
                IReadOnlyList<Visit> sentAway = null;
                var raised = 0;
                shift.TimeUp += v => { sentAway = v; raised++; };

                shift.Tick(59);
                Assert.IsFalse(shift.IsTimeUp);
                shift.Tick(60);
                shift.Tick(61);

                Assert.IsTrue(shift.IsTimeUp);
                Assert.AreEqual(1, raised, "raised once");
                CollectionAssert.AreEqual(new[] { second, third }, sentAway);
                CollectionAssert.AreEqual(new[] { atCounter }, shift.QueuedVisits);
                Assert.IsFalse(shift.CanEnqueue, "nobody new joins");
                Assert.IsFalse(shift.IsOver, "the customer at the counter is still being served");

                foreach (var box in atCounter.Order.Boxes) shift.Desk.Handover(box, 62);
                Assert.IsTrue(shift.HasOrdersLeft);
                Assert.IsTrue(shift.IsOver, "orders left in the warehouse stay unserved");
            }
        }

        [Test]
        public void WhenTimeRunsOut_WithNobodyAtTheCounter_TheShiftIsOver_AndNoDeliveryComes()
        {
            var supply = new SupplyPlan(1, new[] { new BoxRecord(new BoxId("BOX-101"), "3307") });
            using (var shift = new Shift(TestData.MvpCatalog(), new SystemRandomSource(7), null, supply) { TimeLimitSeconds = 30 })
            {
                shift.Start(0);
                shift.TryEnqueue(out var visit);
                shift.OpenNext(1);
                foreach (var box in visit.Order.Boxes) shift.Desk.Handover(box, 2);
                Assert.IsTrue(shift.IsDeliveryDue, "one order served: the delivery would come now");

                shift.Tick(30);
                Assert.IsTrue(shift.IsOver);
                Assert.IsFalse(shift.IsDeliveryDue);
                Assert.IsFalse(shift.TryTakeDueDelivery(out _));
            }
        }

        [Test]
        public void ClockDoesNotRun_UntilStarted()
        {
            using (var shift = TimedShift(10))
            {
                shift.Tick(500);
                Assert.IsFalse(shift.IsTimeUp);
            }
        }
    }
}
