using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Pvz.Core.Tests
{
    public class SupplyTests
    {
        private static readonly BoxId Supply1 = new BoxId("BOX-101");
        private static readonly BoxId Supply2 = new BoxId("BOX-102");

        private Shift _shift;

        [SetUp]
        public void SetUp()
        {
            _shift = new Shift(new BoxCatalog(new[] { new BoxRecord(TestData.Box1, "1435") }), new FakeRandom(0, 0, 0, 0));
            _shift.ExpectDelivery(new[] { new BoxRecord(Supply1, "3307"), new BoxRecord(Supply2, "4410") });
        }

        [TearDown]
        public void TearDown() => _shift.Dispose();

        [Test]
        public void DeliveredBoxes_AreNotInWarehouse_UntilScanned()
        {
            Assert.IsFalse(_shift.Desk.Catalog.Contains(Supply1));
            Assert.IsTrue(_shift.IsPendingIntake(Supply1));
            Assert.AreEqual(2, _shift.PendingIntakeCount);
            Assert.AreEqual(3, _shift.OrdersTotal);
        }

        [Test]
        public void FirstScan_ReceivesTheBox_AndScoresIntake()
        {
            BoxRecord received = null;
            _shift.BoxReceived += r => received = r;

            var outcome = _shift.Scan(Supply1);

            Assert.AreEqual(ScanStatus.Received, outcome.Status);
            Assert.AreEqual("3307", outcome.BoxOrderNumber);
            Assert.AreEqual(Supply1, received.Id);
            Assert.IsTrue(_shift.Desk.Catalog.Contains(Supply1));
            Assert.AreEqual(1, _shift.Stats.BoxesReceived);
            Assert.AreEqual(10, _shift.Stats.Score);

            // A second scan is an ordinary scan now.
            Assert.AreEqual(ScanStatus.NoActiveOrder, _shift.Scan(Supply1).Status);
        }

        [Test]
        public void UnreceivedBox_CannotBeHandedOver()
        {
            _shift.Desk.OpenOrder(TestData.OrderFor(TestData.Box1, "1435"), 0);
            Assert.AreEqual(HandoverStatus.UnknownBox, _shift.Desk.Handover(Supply1, 1).Status);
            Assert.AreEqual(0, _shift.Stats.WrongHandovers);
        }

        [Test]
        public void UnreceivedBoxes_AreNeverOrdered()
        {
            var order = _shift.NextOrder();
            Assert.AreEqual("1435", order.Number);
        }

        [Test]
        public void ShiftIsNotOver_WhileDeliveryIsNotReceived()
        {
            _shift.Desk.OpenOrder(_shift.NextOrder(), 0);
            _shift.Desk.Handover(TestData.Box1, 1);
            Assert.IsFalse(_shift.HasOrdersLeft);
            Assert.IsFalse(_shift.IsOver, "pending supply must keep the shift going");

            _shift.Scan(Supply1);
            _shift.Scan(Supply2);
            var served = 0;
            while (_shift.HasOrdersLeft)
            {
                var order = _shift.NextOrder();
                _shift.Desk.OpenOrder(order, 10 + served);
                _shift.Desk.Handover(order.Boxes[0], 11 + served);
                served++;
            }
            Assert.AreEqual(2, served);
            Assert.IsTrue(_shift.IsOver);
            Assert.AreEqual(3, _shift.Stats.OrdersCompleted);
        }

        [Test]
        public void ExpectDelivery_RejectsDuplicates()
        {
            Assert.Throws<ArgumentException>(() => _shift.ExpectDelivery(new[] { new BoxRecord(Supply1, "9999") }));
            Assert.Throws<ArgumentException>(() => _shift.ExpectDelivery(new[] { new BoxRecord(TestData.Box1, "1435") }));
        }

        [Test]
        public void DeliveryExpected_IsRaised()
        {
            IReadOnlyList<BoxRecord> announced = null;
            _shift.DeliveryExpected += d => announced = d;
            _shift.ExpectDelivery(new[] { new BoxRecord(new BoxId("BOX-103"), "6021") });
            Assert.AreEqual(1, announced.Count);
        }
    }

    public class QrPatternTests
    {
        [Test]
        public void SameId_GivesSamePattern_DifferentIds_Differ()
        {
            var a = QrPattern.Generate("BOX-001");
            var b = QrPattern.Generate("BOX-001");
            var c = QrPattern.Generate("BOX-002");
            CollectionAssert.AreEqual(a, b);
            CollectionAssert.AreNotEqual(a, c);
        }

        [Test]
        public void HasFinderPatternsInThreeCorners()
        {
            var grid = QrPattern.Generate("BOX-123");
            var last = QrPattern.Modules - 1;
            Assert.IsTrue(grid[0, 0] && grid[6, 0] && grid[0, 6]);              // top-left outer ring
            Assert.IsTrue(grid[last, 0] && grid[0, last]);                        // other corners
            Assert.IsFalse(grid[1, 1]);                                           // white gap inside the ring
            Assert.IsTrue(grid[3, 3]);                                            // dark core
            Assert.IsFalse(grid[7, 7]);                                           // separator
        }
    }
}
