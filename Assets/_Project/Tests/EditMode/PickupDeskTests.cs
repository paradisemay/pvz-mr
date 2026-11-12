using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Pvz.Core.Tests
{
    public class PickupDeskTests
    {
        private BoxCatalog _catalog;
        private PickupDesk _desk;

        [SetUp]
        public void SetUp()
        {
            _catalog = TestData.MvpCatalog();
            _desk = new PickupDesk(_catalog);
        }

        [Test]
        public void Scan_WithoutCustomer_ReportsBoxOrderNumber()
        {
            var outcome = _desk.Scan(TestData.Box1);
            Assert.AreEqual(ScanStatus.NoActiveOrder, outcome.Status);
            Assert.AreEqual("1435", outcome.BoxOrderNumber);
        }

        [Test]
        public void Scan_UnknownBox()
        {
            var outcome = _desk.Scan(new BoxId("BOX-999"));
            Assert.AreEqual(ScanStatus.UnknownBox, outcome.Status);
            Assert.IsNull(outcome.BoxOrderNumber);
        }

        [Test]
        public void Scan_MatchesAndOtherOrder()
        {
            _desk.OpenOrder(TestData.OrderFor(TestData.Box2, "2781"), 0);

            Assert.AreEqual(ScanStatus.Matches, _desk.Scan(TestData.Box2).Status);
            var other = _desk.Scan(TestData.Box1);
            Assert.AreEqual(ScanStatus.OtherOrder, other.Status);
            Assert.AreEqual("1435", other.BoxOrderNumber);
        }

        [Test]
        public void OpenOrder_WhileServing_Throws()
        {
            _desk.OpenOrder(TestData.OrderFor(TestData.Box1, "1435"), 0);
            Assert.Throws<InvalidOperationException>(() => _desk.OpenOrder(TestData.OrderFor(TestData.Box2, "2781"), 1));
        }

        [Test]
        public void OpenOrder_WithBoxNotInCatalog_Throws()
        {
            Assert.Throws<ArgumentException>(() => _desk.OpenOrder(TestData.OrderFor(new BoxId("BOX-404"), "404"), 0));
        }

        [Test]
        public void Handover_CorrectBox_CompletesOrderAndRemovesBox()
        {
            var order = TestData.OrderFor(TestData.Box3, "5812");
            _desk.OpenOrder(order, 10);
            OrderResult? completed = null;
            _desk.OrderCompleted += r => completed = r;

            var outcome = _desk.Handover(TestData.Box3, 25);

            Assert.AreEqual(HandoverStatus.Accepted, outcome.Status);
            Assert.IsTrue(outcome.OrderCompleted);
            Assert.IsFalse(_desk.HasActiveOrder);
            Assert.IsFalse(_catalog.Contains(TestData.Box3));
            Assert.IsTrue(completed.HasValue);
            Assert.AreSame(order, completed.Value.Order);
            Assert.AreEqual(15, completed.Value.DurationSeconds, 1e-9);
            Assert.AreEqual(0, completed.Value.Mistakes);
        }

        [Test]
        public void Handover_WrongBox_KeepsOrderAndCountsMistake()
        {
            _desk.OpenOrder(TestData.OrderFor(TestData.Box1, "1435"), 0);
            var wrongEvents = 0;
            _desk.WrongHandover += _ => wrongEvents++;

            var outcome = _desk.Handover(TestData.Box2, 5);

            Assert.AreEqual(HandoverStatus.WrongBox, outcome.Status);
            Assert.IsTrue(_desk.HasActiveOrder);
            Assert.IsTrue(_catalog.Contains(TestData.Box2));
            Assert.AreEqual(1, _desk.ActiveSession.Mistakes);
            Assert.AreEqual(1, wrongEvents);

            OrderResult? completed = null;
            _desk.OrderCompleted += r => completed = r;
            _desk.Handover(TestData.Box1, 9);
            Assert.AreEqual(1, completed.Value.Mistakes);
            Assert.IsNull(_desk.ActiveSession);
        }

        [Test]
        public void Handover_WithoutCustomer()
        {
            Assert.AreEqual(HandoverStatus.NoActiveOrder, _desk.Handover(TestData.Box1, 0).Status);
            Assert.IsTrue(_catalog.Contains(TestData.Box1));
        }

        [Test]
        public void Handover_UnknownBox()
        {
            _desk.OpenOrder(TestData.OrderFor(TestData.Box1, "1435"), 0);
            Assert.AreEqual(HandoverStatus.UnknownBox, _desk.Handover(new BoxId("BOX-777"), 0).Status);
            Assert.AreEqual(0, _desk.ActiveSession.Mistakes);
        }

        [Test]
        public void OpenOrder_WithBoxOfAnotherOrder_Throws()
        {
            Assert.Throws<ArgumentException>(() => _desk.OpenOrder(TestData.OrderFor(TestData.Box2, "1435"), 0));
            Assert.IsFalse(_desk.HasActiveOrder);
        }

        [Test]
        public void WrongHandover_NotRaised_ForUnknownOrNoCustomer()
        {
            var wrong = 0;
            _desk.WrongHandover += _ => wrong++;
            _desk.Handover(TestData.Box1, 0);                 // no customer
            _desk.OpenOrder(TestData.OrderFor(TestData.Box1, "1435"), 0);
            _desk.Handover(new BoxId("BOX-999"), 0);          // unknown box
            Assert.AreEqual(0, wrong);
        }

        [Test]
        public void WrongBox_RaisesHandedOverThenWrongHandover_Once()
        {
            var log = new List<string>();
            _desk.BoxHandedOver += h => log.Add("handover " + h.Status);
            _desk.WrongHandover += h => log.Add("wrong");
            _desk.OpenOrder(TestData.OrderFor(TestData.Box1, "1435"), 0);
            _desk.Handover(TestData.Box2, 1);
            CollectionAssert.AreEqual(new[] { "handover WrongBox", "wrong" }, log);
        }

        [Test]
        public void PartialHandover_DoesNotComplete()
        {
            _catalog.Add(new BoxRecord(new BoxId("BOX-004"), "1435"));
            var completed = 0;
            _desk.OrderCompleted += _ => completed++;
            _desk.OpenOrder(new Order("1435", new[] { TestData.Box1, new BoxId("BOX-004") }), 0);
            _desk.Handover(TestData.Box1, 1);
            Assert.AreEqual(0, completed);
            Assert.AreEqual(1, _desk.ActiveSession.RemainingCount);
        }

        [Test]
        public void TwoOrdersInARow()
        {
            _desk.OpenOrder(TestData.OrderFor(TestData.Box1, "1435"), 0);
            _desk.Handover(TestData.Box1, 5);
            _desk.OpenOrder(TestData.OrderFor(TestData.Box3, "5812"), 6);
            Assert.AreEqual(ScanStatus.Matches, _desk.Scan(TestData.Box3).Status);
            Assert.IsTrue(_desk.Handover(TestData.Box3, 9).OrderCompleted);
            Assert.AreEqual(1, _catalog.Count);
        }

        [Test]
        public void Handlers_CanOpenNextOrder_FromCompletionEvents()
        {
            _desk.OrderCompleted += r =>
            {
                if (r.Order.Number == "1435") _desk.OpenOrder(TestData.OrderFor(TestData.Box2, "2781"), 10);
            };
            _desk.OpenOrder(TestData.OrderFor(TestData.Box1, "1435"), 0);

            Assert.DoesNotThrow(() => _desk.Handover(TestData.Box1, 5));
            Assert.AreEqual("2781", _desk.ActiveOrder.Number);
        }

        [Test]
        public void HandedOverHandler_SeesCommittedState()
        {
            bool? hadActiveOrder = null;
            _desk.BoxHandedOver += _ => hadActiveOrder = _desk.HasActiveOrder;
            _desk.OpenOrder(TestData.OrderFor(TestData.Box1, "1435"), 0);
            _desk.Handover(TestData.Box1, 1);
            Assert.AreEqual(false, hadActiveOrder);
        }

        [Test]
        public void Handover_MultiBoxOrder_CompletesAfterLastBox()
        {
            _catalog.Add(new BoxRecord(new BoxId("BOX-004"), "1435"));
            var order = new Order("1435", new[] { TestData.Box1, new BoxId("BOX-004") });
            _desk.OpenOrder(order, 0);

            var first = _desk.Handover(TestData.Box1, 1);
            Assert.AreEqual(HandoverStatus.Accepted, first.Status);
            Assert.IsFalse(first.OrderCompleted);
            Assert.IsTrue(_desk.HasActiveOrder);

            // Same box again is no longer part of the order (and no longer in the catalog).
            Assert.AreEqual(HandoverStatus.UnknownBox, _desk.Handover(TestData.Box1, 2).Status);

            var last = _desk.Handover(new BoxId("BOX-004"), 3);
            Assert.IsTrue(last.OrderCompleted);
            Assert.IsFalse(_desk.HasActiveOrder);
        }

        [Test]
        public void Events_AreRaisedInOrder()
        {
            var log = new List<string>();
            _desk.OrderOpened += o => log.Add("opened " + o.Number);
            _desk.BoxScanned += s => log.Add("scanned " + s.Status);
            _desk.BoxHandedOver += h => log.Add("handover " + h.Status);
            _desk.OrderCompleted += r => log.Add("completed " + r.Order.Number);

            _desk.OpenOrder(TestData.OrderFor(TestData.Box1, "1435"), 0);
            _desk.Scan(TestData.Box1);
            _desk.Handover(TestData.Box1, 1);

            CollectionAssert.AreEqual(new[]
            {
                "opened 1435", "scanned Matches", "handover Accepted", "completed 1435",
            }, log);
        }
    }
}
