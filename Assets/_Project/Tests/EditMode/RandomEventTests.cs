using System;
using NUnit.Framework;

namespace Pvz.Core.Tests
{
    /// <summary>T014: urgent customers and customers who forgot the order number.</summary>
    public class RandomEventTests
    {
        [Test]
        public void Generator_RollsUrgency_ByChance()
        {
            var generator = new OrderGenerator(new FakeRandom(0, 10, 0, 60)) { UrgentChancePercent = 50 };
            Assert.IsTrue(generator.CreateRandomOrder(TestData.MvpCatalog()).IsUrgent, "10 < 50");
            Assert.IsFalse(generator.CreateRandomOrder(TestData.MvpCatalog()).IsUrgent, "60 >= 50");
        }

        [Test]
        public void ZeroChances_DoNotConsumeRandomness()
        {
            // Only the box index is drawn, so shifts without events keep their old order sequence.
            var generator = new OrderGenerator(new FakeRandom(1, 2));
            var first = generator.CreateRandomOrder(TestData.MvpCatalog());
            var second = generator.CreateRandomOrder(TestData.MvpCatalog());
            Assert.AreEqual("2781", first.Number);
            Assert.AreEqual("5812", second.Number);
            Assert.IsFalse(first.IsUrgent || first.ForgotNumber || second.IsUrgent || second.ForgotNumber);
        }

        [Test]
        public void ForgetfulCustomer_RemembersTheLastTwoDigits()
        {
            var generator = new OrderGenerator(new FakeRandom(0, 0)) { ForgotNumberChancePercent = 100 };
            var order = generator.CreateRandomOrder(TestData.MvpCatalog());
            Assert.IsTrue(order.ForgotNumber);
            Assert.AreEqual("1435", order.Number);
            Assert.AreEqual("35", order.NumberHint);
        }

        [Test]
        public void NumberIsNotForgotten_WhenItsEndIsShared_ByAnotherOrder()
        {
            var catalog = new BoxCatalog(new[]
            {
                new BoxRecord(TestData.Box1, "1435"),
                new BoxRecord(TestData.Box2, "2735"), // same «35»: the hint would not name one order
            });
            var generator = new OrderGenerator(new FakeRandom(0, 0)) { ForgotNumberChancePercent = 100 };
            Assert.IsFalse(generator.CreateRandomOrder(catalog).ForgotNumber);
        }

        [Test]
        public void NumberIsNotForgotten_WhenAPlannedDeliveryBringsTheSameEnding()
        {
            var supply = new SupplyPlan(1, new[] { new BoxRecord(new BoxId("BOX-101"), "7735") });
            using (var shift = new Shift(new BoxCatalog(new[] { new BoxRecord(TestData.Box1, "1435") }),
                       new SystemRandomSource(5), null, supply) { ForgotNumberChancePercent = 100 })
            {
                Assert.IsTrue(shift.TryEnqueue(out var visit));
                Assert.IsFalse(visit.Order.ForgotNumber, "«35» will also name order 7735 once the delivery is scanned in");
            }
        }

        [Test]
        public void UrgentPatienceFactor_MustBeAShare()
        {
            using (var shift = UrgentShift())
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => shift.UrgentPatienceFactor = 0);
                Assert.Throws<ArgumentOutOfRangeException>(() => shift.UrgentPatienceFactor = 1.5);
            }
        }

        [Test]
        public void ShortNumber_CannotBeHalfForgotten()
        {
            Assert.IsFalse(Order.CanHint("42"));
            Assert.Throws<ArgumentException>(() => new Order("42", new[] { TestData.Box1 }, forgotNumber: true));
            var generator = new OrderGenerator(new FakeRandom(0, 0)) { ForgotNumberChancePercent = 100 };
            Assert.IsFalse(generator.CreateRandomOrder(new BoxCatalog(new[] { new BoxRecord(TestData.Box1, "42") })).ForgotNumber);
        }

        private static Shift UrgentShift() =>
            new Shift(TestData.MvpCatalog(), new SystemRandomSource(3)) { PatienceSeconds = 100, UrgentChancePercent = 100 };

        [Test]
        public void UrgentCustomer_HasLessPatience()
        {
            using (var shift = UrgentShift())
            {
                Assert.IsTrue(shift.TryEnqueue(out var visit));
                Assert.IsTrue(visit.Order.IsUrgent);
                Order left = null;
                shift.OrderAbandoned += o => left = o;
                shift.OpenNext(0);

                Assert.AreEqual(60, shift.PatienceFor(visit.Order), 1e-9);
                Assert.AreEqual(0.5, shift.PatienceRemaining(30).Value, 1e-9);
                shift.Tick(59);
                Assert.IsNull(left, "still waiting");
                shift.Tick(60);
                Assert.AreSame(visit.Order, left, "an urgent customer leaves after 60% of the usual patience");
            }
        }

        [Test]
        public void UrgentOrder_GivesABonus_AndIsCounted()
        {
            using (var shift = UrgentShift())
            {
                shift.TryEnqueue(out var visit);
                shift.OpenNext(0);
                foreach (var box in visit.Order.Boxes) shift.Desk.Handover(box, 1);

                Assert.AreEqual(1, shift.Stats.UrgentServed);
                Assert.AreEqual(100 + 50 + 50 + 50, shift.Stats.Score, "order + flawless + fast + urgent");
                Assert.AreEqual(shift.Stats.Score, shift.Stats.LastOrderPoints);
            }
        }

        [Test]
        public void UseRandomSource_KeepsTheEventChances()
        {
            using (var shift = UrgentShift())
            {
                shift.ForgotNumberChancePercent = 40;
                shift.UseRandomSource(new SystemRandomSource(9));
                Assert.AreEqual(100, shift.UrgentChancePercent);
                Assert.AreEqual(40, shift.ForgotNumberChancePercent);
            }
        }
    }
}
