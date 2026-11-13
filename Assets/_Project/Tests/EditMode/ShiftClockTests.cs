using System;
using NUnit.Framework;

namespace Pvz.Core.Tests
{
    public class ShiftClockTests
    {
        [Test]
        public void RunsOut_ExactlyOnce()
        {
            var clock = new ShiftClock { LimitSeconds = 10 };
            clock.Start(0);
            Assert.IsFalse(clock.TryRunOut(9.9));
            Assert.IsTrue(clock.TryRunOut(10));
            Assert.IsFalse(clock.TryRunOut(11), "only the first time");
            Assert.IsTrue(clock.HasRunOut);
        }

        [Test]
        public void NeverRunsOut_WithoutALimit_OrBeforeStart()
        {
            var unlimited = new ShiftClock();
            unlimited.Start(0);
            Assert.IsFalse(unlimited.TryRunOut(1e9));

            var notStarted = new ShiftClock { LimitSeconds = 5 };
            Assert.IsFalse(notStarted.TryRunOut(100));
            Assert.IsNull(notStarted.TimeLeft(100));
        }

        [Test]
        public void NegativeLimit_IsRejected() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShiftClock { LimitSeconds = -1 });
    }
}
