using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Pvz.Core.Tests
{
    /// <summary>T044 (#84): longer shifts — more orders in stock, customers by the clock, a line that grows.</summary>
    public class LongShiftTests
    {
        private sealed class Fixed : IRandomSource
        {
            private readonly int _value;
            public Fixed(int value) => _value = value;
            public int Range(int minInclusive, int maxExclusive) => System.Math.Max(minInclusive, System.Math.Min(maxExclusive - 1, _value));
        }

        [Test]
        public void Customers_ComeByTheClock_NotWhenTheLineHasRoom()
        {
            var clock = new ArrivalClock(start: 100, firstDelay: 2, interval: 20, idleDelay: 2, jitter: 0, random: new Fixed(0));
            Assert.IsFalse(clock.IsDue(101, idleSince: null));
            Assert.IsTrue(clock.IsDue(102, null), "the first one after the first delay");
            clock.Arrived(102);
            Assert.IsFalse(clock.IsDue(121, null), "the counter is busy: the next one keeps to the interval");
            Assert.IsTrue(clock.IsDue(122, null));
        }

        [Test]
        public void AnEmptyCounter_DoesNotKeepAQuickPlayerWaiting()
        {
            var clock = new ArrivalClock(0, 2, 20, idleDelay: 2, jitter: 0, random: new Fixed(0));
            clock.Arrived(2);
            Assert.IsFalse(clock.IsDue(9, idleSince: 8));
            Assert.IsTrue(clock.IsDue(10, idleSince: 8), "nobody at the counter or in line for 2 s: somebody comes");
        }

        [Test]
        public void Arrivals_WanderWithinTheJitter()
        {
            var early = new ArrivalClock(0, 0, 20, 2, jitter: 0.2, random: new Fixed(-100));
            early.Arrived(0);
            Assert.AreEqual(16, early.NextArrival, 1e-9);
            var late = new ArrivalClock(0, 0, 20, 2, jitter: 0.2, random: new Fixed(100));
            late.Arrived(0);
            Assert.AreEqual(24, late.NextArrival, 1e-9);
        }

        private static List<BoxRecord> Stock() => new List<BoxRecord>
        {
            new BoxRecord(new BoxId("BOX-001"), "1435"),
            new BoxRecord(new BoxId("BOX-002"), "2781"),
            new BoxRecord(new BoxId("BOX-003"), "5812"),
            new BoxRecord(new BoxId("BOX-201"), "8804"), // spare
        };

        private static List<BoxRecord> Supply() => new List<BoxRecord>
        {
            new BoxRecord(new BoxId("BOX-101"), "3307"),
            new BoxRecord(new BoxId("BOX-102"), "3307"),
            new BoxRecord(new BoxId("BOX-103"), "2718"),
        };

        [TestCase(6)]
        [TestCase(10)]
        [TestCase(14)]
        public void TheStock_IsFilledUp_ToTheOrdersOfTheDifficulty(int target)
        {
            var notForSale = new HashSet<string> { "8804" };
            var extra = StockPlan.Extra(Stock(), Supply(), notForSale, target, new SystemRandomSource(7));
            var sold = Stock().Concat(Supply()).Concat(extra).Select(b => b.OrderNumber).Where(n => !notForSale.Contains(n)).Distinct().Count();
            Assert.AreEqual(target, sold);
            Assert.IsTrue(extra.All(b => b.Id.Value.StartsWith(StockPlan.GeneratedPrefix)));
            Assert.AreEqual(extra.Count, extra.Select(b => b.Id).Distinct().Count(), "every generated box is unique");
            var given = Stock().Concat(Supply()).Select(b => b.OrderNumber).ToList();
            foreach (var number in extra.Select(b => b.OrderNumber).Distinct())
            {
                CollectionAssert.DoesNotContain(given, number);
                Assert.IsFalse(given.Any(n => OrderNumbers.LookAlike(n, number)), $"{number}: no random look-alike traps");
                Assert.LessOrEqual(extra.Count(b => b.OrderNumber == number), 2, "one or two boxes an order");
            }
        }

        [Test]
        public void EnoughStock_AddsNothing_AndTheSameSeed_TheSameStock()
        {
            Assert.IsEmpty(StockPlan.Extra(Stock(), Supply(), new HashSet<string> { "8804" }, 3, new SystemRandomSource(1)));
            var a = StockPlan.Extra(Stock(), Supply(), null, 12, new SystemRandomSource(42));
            var b = StockPlan.Extra(Stock(), Supply(), null, 12, new SystemRandomSource(42));
            CollectionAssert.AreEqual(a.Select(x => x.Id.Value + x.OrderNumber), b.Select(x => x.Id.Value + x.OrderNumber));
        }

        [Test]
        public void AFullLine_TurnsNewcomersAway_AndTheirOrdersStay()
        {
            var catalog = new BoxCatalog(Stock().Take(3));
            var shift = new Shift(catalog, new SystemRandomSource(3)) { QueueLength = 1 };
            Assert.IsTrue(shift.TryEnqueue(out _));
            Assert.IsTrue(shift.IsLineFull);
            Assert.IsFalse(shift.CanEnqueue);
            shift.TurnAway();
            Assert.AreEqual(1, shift.Stats.CustomersTurnedAway);
            Assert.IsTrue(shift.HasUnreservedOrders, "the turned-away customer's order is still for sale");
        }
    }
}
