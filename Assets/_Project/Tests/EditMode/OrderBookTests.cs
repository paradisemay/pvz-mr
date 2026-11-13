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
        public void OpeningTheDeskDirectly_AlsoMarksTheVisitAtTheCounter_BeforeOthersHear()
        {
            using (var shift = NewShift())
            {
                var order = shift.NextOrder();
                var stateWhenOpened = VisitState.Waiting;
                shift.Desk.OrderOpened += _ => stateWhenOpened = shift.Book.Entries[0].State; // like the presenter
                shift.Desk.OpenOrder(order, 0);
                Assert.AreEqual(VisitState.AtCounter, stateWhenOpened, "the book is updated before later subscribers react");
            }
        }

        [Test]
        public void IllegalTransitions_Throw()
        {
            var book = new OrderBook();
            var visit = Visit.Pickup(TestData.OrderFor(TestData.Box1, "1435"));
            Assert.Throws<System.InvalidOperationException>(() => book.Open(visit), "not in the book");
            book.Join(visit);
            Assert.Throws<System.InvalidOperationException>(() => book.Join(visit), "twice");
            Assert.Throws<System.InvalidOperationException>(() => book.Serve(visit), "served before reaching the counter");
            Assert.Throws<System.InvalidOperationException>(() => book.Abandon(visit), "abandoned while still in line");
            book.Open(visit);
            Assert.Throws<System.InvalidOperationException>(() => book.Leave(new[] { visit }), "left the line from the counter");
            book.Serve(visit);
            Assert.Throws<System.InvalidOperationException>(() => book.Open(visit), "a finished visit stays finished");
        }

        [Test]
        public void TheOpenVisits_AreAlwaysTheLine()
        {
            using (var shift = NewShift())
            {
                // The line kept by the book must match the states of its entries.
                void Check(string when) => CollectionAssert.AreEqual(
                    shift.Book.Entries.Where(e => e.IsOpen).Select(e => e.Visit).ToList(), shift.QueuedVisits, when);
                shift.TimeLimitSeconds = 50;
                shift.Start(0);
                shift.TryEnqueue(out var a); shift.TryEnqueue(out var b); shift.TryEnqueue(out _);
                Check("joined");
                shift.OpenNext(0); Check("at the counter");
                foreach (var box in a.Order.Boxes) shift.Desk.Handover(box, 1);
                Check("served");
                shift.LeaveQueue(b); Check("left the line");
                shift.OpenNext(2); shift.Tick(102); Check("abandoned");
                shift.TryEnqueue(out _); shift.Tick(150); Check("time up");
            }
        }

        [Test]
        public void TimeUp_ChangesTheBookOnce()
        {
            using (var shift = NewShift())
            {
                shift.TimeLimitSeconds = 10;
                shift.Start(0);
                shift.TryEnqueue(out _); shift.TryEnqueue(out _); shift.TryEnqueue(out _);
                var changes = 0;
                shift.Book.Changed += () => changes++;
                shift.Tick(10);
                Assert.AreEqual(1, changes, "three customers went home: one change");
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
