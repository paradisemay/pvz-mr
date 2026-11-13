using NUnit.Framework;

namespace Pvz.Core.Tests
{
    public class PatienceTests
    {
        private Shift _shift;

        [SetUp]
        public void SetUp()
        {
            _shift = new Shift(TestData.MvpCatalog(), new FakeRandom(0, 0, 0, 0)) { PatienceSeconds = 100 };
        }

        [TearDown]
        public void TearDown() => _shift.Dispose();

        [Test]
        public void CustomerHurries_Once_AtSixtyPercent()
        {
            var hurried = 0;
            _shift.CustomerHurried += _ => hurried++;
            _shift.Desk.OpenOrder(_shift.NextOrder(), 0);

            _shift.Tick(59);
            Assert.AreEqual(0, hurried);
            _shift.Tick(60);
            _shift.Tick(70);
            Assert.AreEqual(1, hurried);
            Assert.IsTrue(_shift.Desk.HasActiveOrder);
        }

        [Test]
        public void CustomerLeaves_WhenPatienceRunsOut_AndOrderStaysInWarehouse()
        {
            Order abandoned = null;
            _shift.OrderAbandoned += o => abandoned = o;
            var order = _shift.NextOrder();
            _shift.Desk.OpenOrder(order, 10);

            _shift.Tick(110);

            Assert.AreSame(order, abandoned);
            Assert.IsFalse(_shift.Desk.HasActiveOrder);
            Assert.IsTrue(_shift.Desk.Catalog.Contains(order.Boxes[0]), "boxes wait for the next customer");
            Assert.AreEqual(1, _shift.Stats.OrdersAbandoned);
            Assert.AreEqual(0, _shift.Stats.OrdersCompleted);
            Assert.IsFalse(_shift.IsOver);
        }

        [Test]
        public void AbandonPenalty_IsApplied()
        {
            _shift.Desk.OpenOrder(_shift.NextOrder(), 0);
            _shift.Desk.Handover(_shift.Desk.ActiveOrder.Boxes[0], 5); // +200
            _shift.Desk.OpenOrder(_shift.NextOrder(), 10);
            _shift.Tick(200);
            Assert.AreEqual(200 - 50, _shift.Stats.Score);
        }

        [Test]
        public void ZeroPatience_MeansCustomersWaitForever()
        {
            _shift.PatienceSeconds = 0;
            _shift.Desk.OpenOrder(_shift.NextOrder(), 0);
            _shift.Tick(100000);
            Assert.IsTrue(_shift.Desk.HasActiveOrder);
        }

        [Test]
        public void Tick_WithoutCustomer_DoesNothing()
        {
            Assert.DoesNotThrow(() => _shift.Tick(1000));
        }

        [Test]
        public void Rating_CountsAbandonedCustomersAsMistakes()
        {
            Assert.AreEqual(ShiftGrade.Good, ShiftRating.Grade(3, 3, 0, abandoned: 1));
        }
    }
}
