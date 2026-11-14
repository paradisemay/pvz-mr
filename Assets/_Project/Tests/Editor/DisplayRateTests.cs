using NUnit.Framework;
using Pvz.Runtime;

namespace Pvz.Editor.Tests
{
    /// <summary>T028: which display rate the game asks the headset for.</summary>
    public class DisplayRateTests
    {
        [Test]
        public void Quest3Rates_Give90()
        {
            Assert.AreEqual(90f, DisplayRate.Pick(new[] { 72f, 80f, 90f, 120f }));
        }

        [Test]
        public void Without90_TheHighestRateBelowIsTaken()
        {
            Assert.AreEqual(80f, DisplayRate.Pick(new[] { 120f, 72f, 80f }));
        }

        [Test]
        public void NothingUsable_KeepsTheCurrentRate()
        {
            Assert.IsNull(DisplayRate.Pick(new float[0]));
            Assert.IsNull(DisplayRate.Pick(null));
            Assert.IsNull(DisplayRate.Pick(new[] { 120f }), "never above the preferred rate");
        }

        [Test]
        public void RatesReportedWithFractions_StillMatch()
        {
            Assert.AreEqual(90.0001f, DisplayRate.Pick(new[] { 72f, 90.0001f }));
        }
    }
}
