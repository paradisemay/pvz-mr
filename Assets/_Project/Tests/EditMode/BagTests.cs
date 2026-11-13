using NUnit.Framework;

namespace Pvz.Core.Tests
{
    public class BagTests
    {
        private BoxCatalog _catalog;
        private PickupDesk _desk;
        private ShiftStats _stats;

        [SetUp]
        public void SetUp()
        {
            _catalog = TestData.MvpCatalog();
            _desk = new PickupDesk(_catalog);
            _stats = new ShiftStats(_desk);
        }

        [TearDown]
        public void TearDown() => _stats.Dispose();

        private static Order WithBag(BoxId box, string number) => new Order(number, new[] { box }, needsBag: true);

        [Test]
        public void OrderNeedingBag_CompletesOnlyAfterTheBag()
        {
            var completed = 0;
            _desk.OrderCompleted += _ => completed++;
            _desk.OpenOrder(WithBag(TestData.Box1, "1435"), 0);

            var box = _desk.Handover(TestData.Box1, 5);
            Assert.AreEqual(HandoverStatus.Accepted, box.Status);
            Assert.IsFalse(box.OrderCompleted);
            Assert.IsTrue(_desk.ActiveSession.AwaitingBag);
            Assert.AreEqual(0, completed);

            Assert.AreEqual(BagStatus.Accepted, _desk.HandOverBag(8));
            Assert.AreEqual(1, completed);
            Assert.IsFalse(_desk.HasActiveOrder);
            Assert.AreEqual(1, _stats.BagsGiven);
            Assert.AreEqual(100 + 50 + 50 + 20, _stats.Score);
        }

        [Test]
        public void BagBeforeTheBoxes_IsTooEarly_AndNotCounted()
        {
            _desk.OpenOrder(WithBag(TestData.Box1, "1435"), 0);
            Assert.AreEqual(BagStatus.TooEarly, _desk.HandOverBag(1));
            Assert.AreEqual(0, _stats.BagsGiven);
            Assert.IsFalse(_desk.Handover(TestData.Box1, 2).OrderCompleted);
            Assert.IsTrue(_desk.ActiveSession.AwaitingBag);
        }

        [Test]
        public void BagRequested_IsRaisedOnce_AfterTheLastBox()
        {
            var requested = 0;
            _desk.BagRequested += _ => requested++;
            _catalog.Add(new BoxRecord(new BoxId("BOX-004"), "1435"));
            _desk.OpenOrder(new Order("1435", new[] { TestData.Box1, new BoxId("BOX-004") }, needsBag: true), 0);

            _desk.Handover(TestData.Box1, 1);
            Assert.AreEqual(0, requested);
            _desk.Handover(new BoxId("BOX-004"), 2);
            Assert.AreEqual(1, requested);
            _desk.HandOverBag(3);
            Assert.AreEqual(1, requested);
        }

        [Test]
        public void BagRequested_IsNotRaised_WhenNoBagIsNeeded()
        {
            var requested = 0;
            _desk.BagRequested += _ => requested++;
            _desk.OpenOrder(TestData.OrderFor(TestData.Box1, "1435"), 0);
            _desk.Handover(TestData.Box1, 1);
            Assert.AreEqual(0, requested);
        }

        [Test]
        public void LastOrderPoints_IncludeTheBag()
        {
            _desk.OpenOrder(WithBag(TestData.Box1, "1435"), 0);
            _desk.Handover(TestData.Box1, 5);
            _desk.HandOverBag(8);
            Assert.AreEqual(100 + 50 + 50 + 20, _stats.LastOrderPoints);
            Assert.AreEqual(220, _stats.Score);
        }

        [Test]
        public void BagNotNeeded_IsNotAMistake()
        {
            _desk.OpenOrder(TestData.OrderFor(TestData.Box1, "1435"), 0);
            Assert.AreEqual(BagStatus.NotNeeded, _desk.HandOverBag(1));
            Assert.AreEqual(0, _desk.ActiveSession.Mistakes);
            Assert.AreEqual(0, _stats.WrongHandovers);
        }

        [Test]
        public void SecondBag_AfterTheOrderClosed_IsNotCounted()
        {
            _desk.OpenOrder(WithBag(TestData.Box1, "1435"), 0);
            _desk.Handover(TestData.Box1, 1);
            Assert.AreEqual(BagStatus.Accepted, _desk.HandOverBag(2));
            Assert.AreEqual(BagStatus.NoActiveOrder, _desk.HandOverBag(3));
            Assert.AreEqual(1, _stats.BagsGiven);
        }

        [Test]
        public void BagWithoutCustomer()
        {
            Assert.AreEqual(BagStatus.NoActiveOrder, _desk.HandOverBag(0));
        }

        [Test]
        public void Generator_DecidesBag_ByChance()
        {
            var always = new OrderGenerator(new FakeRandom(0, 0), bagChancePercent: 100);
            Assert.IsTrue(always.CreateRandomOrder(TestData.MvpCatalog()).NeedsBag);

            var never = new OrderGenerator(new FakeRandom(0), bagChancePercent: 0);
            Assert.IsFalse(never.CreateRandomOrder(TestData.MvpCatalog()).NeedsBag);

            var half = new OrderGenerator(new FakeRandom(0, 60), bagChancePercent: 50);
            Assert.IsFalse(half.CreateRandomOrder(TestData.MvpCatalog()).NeedsBag);
        }

        [Test]
        public void MultiBoxOrderWithBag_TracksRemainingThenBag()
        {
            _catalog.Add(new BoxRecord(new BoxId("BOX-004"), "1435"));
            _desk.OpenOrder(new Order("1435", new[] { TestData.Box1, new BoxId("BOX-004") }, needsBag: true), 0);

            _desk.Handover(TestData.Box1, 1);
            Assert.AreEqual(1, _desk.ActiveSession.RemainingCount);
            Assert.IsFalse(_desk.ActiveSession.AwaitingBag);

            _desk.Handover(new BoxId("BOX-004"), 2);
            Assert.IsTrue(_desk.ActiveSession.AwaitingBag);
            _desk.HandOverBag(3);
            Assert.IsFalse(_desk.HasActiveOrder);
        }
    }
}
