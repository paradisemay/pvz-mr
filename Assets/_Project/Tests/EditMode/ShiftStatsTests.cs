using NUnit.Framework;

namespace Pvz.Core.Tests
{
    public class ShiftStatsTests
    {
        private PickupDesk _desk;
        private ShiftStats _stats;

        [SetUp]
        public void SetUp()
        {
            _desk = new PickupDesk(TestData.MvpCatalog());
            _stats = new ShiftStats(_desk);
        }

        [TearDown]
        public void TearDown() => _stats.Dispose();

        [Test]
        public void FastFlawlessOrder_GetsAllBonuses()
        {
            _desk.OpenOrder(TestData.OrderFor(TestData.Box1, "1435"), 0);
            _desk.Handover(TestData.Box1, 20);

            Assert.AreEqual(100 + 50 + 50, _stats.Score);
            Assert.AreEqual(1, _stats.OrdersCompleted);
            Assert.AreEqual(1, _stats.FlawlessOrders);
            Assert.AreEqual(20, _stats.AverageServiceSeconds, 1e-9);
        }

        [Test]
        public void SlowOrderWithMistake_GetsBasePointsMinusPenalty()
        {
            _desk.OpenOrder(TestData.OrderFor(TestData.Box1, "1435"), 0);
            _desk.Handover(TestData.Box2, 10);   // -25, clamped at 0
            _desk.Handover(TestData.Box1, 120);  // +100

            Assert.AreEqual(100, _stats.Score);
            Assert.AreEqual(1, _stats.WrongHandovers);
            Assert.AreEqual(0, _stats.FlawlessOrders);
        }

        [Test]
        public void MediumSpeed_GetsOkBonus()
        {
            _desk.OpenOrder(TestData.OrderFor(TestData.Box1, "1435"), 0);
            _desk.Handover(TestData.Box1, 45);
            Assert.AreEqual(100 + 50 + 25, _stats.Score);
        }

        [TestCase(30.0, 100 + 50 + 50)]
        [TestCase(30.001, 100 + 50 + 25)]
        [TestCase(60.0, 100 + 50 + 25)]
        [TestCase(60.001, 100 + 50)]
        public void SpeedBonus_Boundaries(double seconds, int expected)
        {
            _desk.OpenOrder(TestData.OrderFor(TestData.Box1, "1435"), 0);
            _desk.Handover(TestData.Box1, seconds);
            Assert.AreEqual(expected, _stats.Score);
        }

        [Test]
        public void Penalty_IsAppliedAfterPositiveScore()
        {
            _desk.OpenOrder(TestData.OrderFor(TestData.Box1, "1435"), 0);
            _desk.Handover(TestData.Box1, 10);   // 200
            _desk.OpenOrder(TestData.OrderFor(TestData.Box2, "2781"), 10);
            _desk.Handover(TestData.Box3, 12);   // 175

            Assert.AreEqual(175, _stats.Score);
        }

        [Test]
        public void Scans_AreCounted()
        {
            _desk.OpenOrder(TestData.OrderFor(TestData.Box1, "1435"), 0);
            _desk.Scan(TestData.Box2);
            _desk.Scan(TestData.Box1);

            Assert.AreEqual(2, _stats.Scans);
            Assert.AreEqual(1, _stats.WrongScans);
        }

        [Test]
        public void BoxesHandedOver_CountsEveryAcceptedBox_NotWrongOnes()
        {
            _desk.OpenOrder(TestData.OrderFor(TestData.Box1, "1435"), 0);
            _desk.Handover(TestData.Box2, 1); // wrong box
            _desk.Handover(TestData.Box1, 2);
            Assert.AreEqual(1, _stats.BoxesHandedOver);
        }

        [Test]
        public void BoxesHandedOver_CountsEachBoxOfAMultiBoxOrder_AndIgnoresUnknownOnes()
        {
            _desk.Catalog.Add(new BoxRecord(new BoxId("BOX-004"), "1435"));
            _desk.Handover(TestData.Box1, 0); // no customer yet
            _desk.OpenOrder(new Order("1435", new[] { TestData.Box1, new BoxId("BOX-004") }), 0);
            _desk.Handover(new BoxId("BOX-999"), 1); // unknown box
            _desk.Handover(TestData.Box1, 2);
            _desk.Handover(new BoxId("BOX-004"), 3);
            Assert.AreEqual(2, _stats.BoxesHandedOver);
        }

        [Test]
        public void Dispose_StopsCounting()
        {
            _stats.Dispose();
            _desk.OpenOrder(TestData.OrderFor(TestData.Box1, "1435"), 0);
            _desk.Handover(TestData.Box1, 1);
            Assert.AreEqual(0, _stats.Score);
        }
    }
}
