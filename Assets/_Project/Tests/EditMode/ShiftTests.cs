using System;
using System.Linq;
using NUnit.Framework;

namespace Pvz.Core.Tests
{
    public class ShiftTests
    {
        private Shift _shift;

        [SetUp]
        public void SetUp() => _shift = new Shift(TestData.MvpCatalog(), new FakeRandom(0, 0, 0));

        [TearDown]
        public void TearDown() => _shift.Dispose();

        [Test]
        public void CountsDistinctOrders()
        {
            var catalog = TestData.MvpCatalog();
            catalog.Add(new BoxRecord(new BoxId("BOX-004"), "1435"));
            using (var shift = new Shift(catalog, new FakeRandom()))
            {
                Assert.AreEqual(3, shift.OrdersTotal);
            }
        }

        [Test]
        public void ServingEveryOrder_EndsTheShift()
        {
            var served = 0;
            while (_shift.HasOrdersLeft)
            {
                var order = _shift.NextOrder();
                _shift.Desk.OpenOrder(order, served * 10);
                foreach (var box in order.Boxes) _shift.Desk.Handover(box, served * 10 + 5);
                served++;
            }

            Assert.AreEqual(3, served);
            Assert.IsTrue(_shift.IsOver);
            Assert.AreEqual(3, _shift.Stats.OrdersCompleted);
            Assert.IsNull(_shift.NextOrder());
        }

        [Test]
        public void IsNotOver_WhileLastCustomerIsServed()
        {
            using (var shift = new Shift(new BoxCatalog(new[] { new BoxRecord(TestData.Box1, "1435") }), new FakeRandom()))
            {
                shift.Desk.OpenOrder(shift.NextOrder(), 0);
                // The box is still in the catalog until handed over, but even after that the order must close first.
                Assert.IsFalse(shift.IsOver);
                shift.Desk.Handover(TestData.Box1, 1);
                Assert.IsTrue(shift.IsOver);
            }
        }

        [Test]
        public void NextOrder_ReservesOrders_SoQueuedCustomersNeverShareOne()
        {
            var shift = new Shift(TestData.MvpCatalog(), new SystemRandomSource(3));
            var numbers = new[] { shift.NextOrder().Number, shift.NextOrder().Number, shift.NextOrder().Number };
            CollectionAssert.AllItemsAreUnique(numbers);
            Assert.IsFalse(shift.HasUnreservedOrders);
            Assert.IsNull(shift.NextOrder(), "every order is already promised to someone");
            shift.Dispose();
        }

        [Test]
        public void CompletedOrder_FreesNothing_AbandonedOrder_GoesBackToThePool()
        {
            var shift = new Shift(TestData.MvpCatalog(), new FakeRandom(0, 0, 0, 0, 0));
            var first = shift.NextOrder();
            var second = shift.NextOrder();
            Assert.IsTrue(shift.IsReserved(first.Number) && shift.IsReserved(second.Number));

            shift.Desk.OpenOrder(first, 0);
            shift.Desk.Handover(first.Boxes[0], 1);
            Assert.IsFalse(shift.IsReserved(first.Number));

            shift.Desk.OpenOrder(second, 2);
            shift.Desk.Abandon();
            Assert.IsFalse(shift.IsReserved(second.Number));
            Assert.IsTrue(shift.HasUnreservedOrders);
            shift.Dispose();
        }

        [Test]
        public void ReleaseReservation_MakesTheOrderAvailableAgain()
        {
            var shift = new Shift(new BoxCatalog(new[] { new BoxRecord(TestData.Box1, "1435") }), new FakeRandom(0, 0));
            var order = shift.NextOrder();
            Assert.IsNull(shift.NextOrder());
            shift.ReleaseReservation(order);
            Assert.AreEqual("1435", shift.NextOrder().Number);
            shift.Dispose();
        }

        [Test]
        public void HandOverWaiting_GivesOnlyRequiredBoxes_WithoutMistakes()
        {
            _shift.Desk.OpenOrder(TestData.OrderFor(TestData.Box2, "2781"), 0);

            var outcomes = _shift.HandOverWaiting(new[] { TestData.Box1, TestData.Box2, TestData.Box3 }, 3);

            Assert.AreEqual(1, outcomes.Count);
            Assert.AreEqual(HandoverStatus.Accepted, outcomes[0].Status);
            Assert.AreEqual(0, _shift.Stats.WrongHandovers);
            Assert.AreEqual(1, _shift.Stats.OrdersCompleted);
            Assert.IsTrue(_shift.Desk.Catalog.Contains(TestData.Box1));
        }

        [Test]
        public void HandOverWaiting_WithoutCustomer_DoesNothing()
        {
            Assert.AreEqual(0, _shift.HandOverWaiting(new[] { TestData.Box1 }, 0).Count);
        }

        [Test]
        public void UseRandomSource_MakesOrderSequenceDeterministic()
        {
            string[] Sequence(int seed)
            {
                using (var shift = new Shift(TestData.MvpCatalog(), new FakeRandom()))
                {
                    shift.UseRandomSource(new SystemRandomSource(seed));
                    return Enumerable.Range(0, 3).Select(i =>
                    {
                        var order = shift.NextOrder();
                        shift.Desk.OpenOrder(order, i);
                        shift.Desk.Handover(order.Boxes[0], i);
                        return order.Number;
                    }).ToArray();
                }
            }

            CollectionAssert.AreEqual(Sequence(7), Sequence(7));
        }
    }
}
