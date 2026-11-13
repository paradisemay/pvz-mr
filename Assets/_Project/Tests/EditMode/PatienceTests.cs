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
        public void CustomerBeingServed_DoesNotLeave()
        {
            // Two-box order: once the first box is handed over, the customer waits for the rest (and the bag).
            using (var shift = new Shift(new BoxCatalog(new[]
                   {
                       new BoxRecord(TestData.Box1, "1435"),
                       new BoxRecord(new BoxId("BOX-004"), "1435"),
                   }), new FakeRandom(0, 0), null, null, bagChancePercent: 100) { PatienceSeconds = 10 })
            {
                shift.Desk.OpenOrder(shift.NextOrder(), 0);
                shift.Desk.Handover(TestData.Box1, 5);
                shift.Tick(1000);
                Assert.IsTrue(shift.Desk.HasActiveOrder);
                Assert.AreEqual(0, shift.Stats.OrdersAbandoned);

                shift.Desk.Handover(new BoxId("BOX-004"), 1001);
                shift.Tick(2000); // waiting for the bag
                Assert.IsTrue(shift.Desk.ActiveSession.AwaitingBag);
                Assert.AreEqual(0, shift.Stats.OrdersAbandoned);
            }
        }

        [Test]
        public void PatienceRemaining_DecreasesFromOneToZero()
        {
            Assert.IsNull(_shift.PatienceRemaining(0), "no customer at the counter");
            var order = _shift.NextOrder();
            _shift.Desk.OpenOrder(order, 10);

            Assert.AreEqual(1.0, _shift.PatienceRemaining(10).Value, 1e-9);
            Assert.AreEqual(0.75, _shift.PatienceRemaining(35).Value, 1e-9);
            Assert.AreEqual(0.0, _shift.PatienceRemaining(500).Value, 1e-9);
        }

        [Test]
        public void PatienceRemaining_IsNull_WithInfinitePatience()
        {
            _shift.Desk.OpenOrder(_shift.NextOrder(), 0);
            _shift.PatienceSeconds = 0;
            Assert.IsNull(_shift.PatienceRemaining(5));
        }

        [Test]
        public void PatienceRemaining_IsNull_OnceTheFirstBoxIsHandedOver()
        {
            using (var shift = new Shift(new BoxCatalog(new[]
                   {
                       new BoxRecord(TestData.Box1, "1435"),
                       new BoxRecord(new BoxId("BOX-004"), "1435"),
                   }), new FakeRandom(0)) { PatienceSeconds = 100 })
            {
                shift.Desk.OpenOrder(shift.NextOrder(), 0);
                shift.Desk.Handover(TestData.Box1, 5);
                Assert.IsTrue(shift.Desk.HasActiveOrder, "a two-box order is still open");
                Assert.IsNull(shift.PatienceRemaining(6), "a customer being served no longer leaves");
            }
        }

        [Test]
        public void PatienceRemaining_AgreesWithTick()
        {
            var hurried = false;
            _shift.CustomerHurried += _ => hurried = true;
            _shift.Desk.OpenOrder(_shift.NextOrder(), 0);

            _shift.Tick(60);
            Assert.IsTrue(hurried);
            Assert.AreEqual(1.0 - Shift.HurryFraction, _shift.PatienceRemaining(60).Value, 1e-9, "hurry happens at the bar's yellow point");

            _shift.Tick(100);
            Assert.IsNull(_shift.PatienceRemaining(100), "the customer left");
            Assert.AreEqual(1, _shift.Stats.OrdersAbandoned);
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
