using NUnit.Framework;
using Pvz.Runtime;

namespace Pvz.Editor.Tests
{
    /// <summary>The shift clock on the board.</summary>
    public class ClockTextTests
    {
        [TestCase(420.0, "7:00")]
        [TestCase(192.0, "3:12")]
        [TestCase(59.2, "1:00")] // rounded up: the clock shows 0:00 only when the time is really up
        [TestCase(0.4, "0:01")]
        [TestCase(0.0, "0:00")]
        public void ShowsMinutesAndSeconds(double secondsLeft, string expected) =>
            Assert.AreEqual(expected, ShiftPresenter.ClockText(secondsLeft));

        [Test]
        public void NoLimit_ShowsNoClock() => Assert.IsNull(ShiftPresenter.ClockText(null));
    }
}
