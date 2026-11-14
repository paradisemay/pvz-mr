using System;
using NUnit.Framework;

namespace Pvz.Core.Tests
{
    /// <summary>T014: urgent customers. (Customers who forgot the order number are gone: the code is scanned from a phone, T035.)</summary>
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
            Assert.IsFalse(first.IsUrgent || second.IsUrgent);
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
                shift.UseRandomSource(new SystemRandomSource(9));
                Assert.AreEqual(100, shift.UrgentChancePercent);
            }
        }
    }
}
