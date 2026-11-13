using System.Linq;
using NUnit.Framework;

namespace Pvz.Core.Tests
{
    /// <summary>T019: every customer of the shift with their state (the order list on the counter).</summary>
    public class OrderBookTests
    {
        private static Shift NewShift(int queue = 3) =>
            new Shift(TestData.MvpCatalog(), new SystemRandomSource(11)) { QueueLength = queue, PatienceSeconds = 100 };

        private static VisitState StateOf(Shift shift, Visit visit) => shift.Book.Find(visit).State;

        [Test]
        public void VisitsMoveFrom_Waiting_ToAtCounter_ToServed()
        {
            using (var shift = NewShift())
            {
                shift.TryEnqueue(out var first);
                shift.TryEnqueue(out var second);
                Assert.AreEqual(VisitState.Waiting, StateOf(shift, first));
                Assert.AreEqual(VisitState.Waiting, StateOf(shift, second));

                shift.OpenNext(0);
                Assert.AreEqual(VisitState.AtCounter, StateOf(shift, first));

                foreach (var box in first.Order.Boxes) shift.Desk.Handover(box, 1);
                Assert.AreEqual(VisitState.Served, StateOf(shift, first));
                Assert.AreEqual(VisitState.Waiting, StateOf(shift, second));
                Assert.AreEqual(1, shift.Book.Count(VisitState.Served));
            }
        }

        [Test]
        public void ACustomerOutOfPatience_IsAbandoned_AndTheSameOrderCanBeOrderedAgain()
        {
            using (var shift = NewShift(queue: 1))
            {
                shift.TryEnqueue(out var impatient);
                shift.OpenNext(0);
                shift.Tick(100);
                Assert.AreEqual(VisitState.Abandoned, StateOf(shift, impatient));

                // The next customer gets a new entry; the old one keeps its final state.
                Assert.IsTrue(shift.TryEnqueue(out var next));
                shift.OpenNext(101);
                Assert.AreEqual(VisitState.AtCounter, StateOf(shift, next));
                Assert.AreEqual(VisitState.Abandoned, StateOf(shift, impatient), "a finished visit keeps its state");
                Assert.AreEqual(2, shift.Book.Entries.Count);
            }
        }

        [Test]
        public void LeavingTheLine_AndTimeRunningOut_AreRecorded()
        {
            using (var shift = NewShift())
            {
                shift.TimeLimitSeconds = 10;
                shift.Start(0);
                shift.TryEnqueue(out var atCounter);
                shift.TryEnqueue(out var leaves);
                shift.TryEnqueue(out var sentHome);
                shift.OpenNext(0);
                shift.LeaveQueue(leaves);
                shift.Tick(10);

                Assert.AreEqual(VisitState.AtCounter, StateOf(shift, atCounter), "served to the end");
                Assert.AreEqual(VisitState.Left, StateOf(shift, leaves));
                Assert.AreEqual(VisitState.Left, StateOf(shift, sentHome));
            }
        }

        [Test]
        public void AcceptedReturn_IsServed()
        {
            using (var shift = NewShift(queue: 2))
            {
                shift.ReturnChancePercent = 0;
                shift.TryEnqueue(out var pickup);
                shift.OpenNext(0);
                shift.Desk.Handover(pickup.Order.Boxes[0], 1);
                shift.ReturnChancePercent = 100;
                shift.TryEnqueue(out var returning);
                Assert.IsTrue(returning.IsReturn);

                shift.OpenNext(5);
                Assert.AreEqual(VisitState.AtCounter, StateOf(shift, returning));
                shift.Scan(returning.Return.Box);
                Assert.AreEqual(VisitState.Served, StateOf(shift, returning));
            }
        }

        [Test]
        public void Changed_IsRaised_AfterTheStateIsUpdated()
        {
            using (var shift = NewShift())
            {
                var seen = new System.Collections.Generic.List<VisitState>();
                shift.Book.Changed += () => seen.Add(shift.Book.Entries.Last().State);
                shift.TryEnqueue(out _);
                shift.OpenNext(0);
                CollectionAssert.AreEqual(new[] { VisitState.Waiting, VisitState.AtCounter }, seen);
            }
        }
    }
}
