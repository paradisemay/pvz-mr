using NUnit.Framework;

namespace Pvz.Core.Tests
{
    /// <summary>One rule for «where a box may be»: saved places and lost boxes agree (T025).</summary>
    public class WarehouseBoundsTests
    {
        [TestCase(0f, 0f, true)]
        [TestCase(6f, 0f, true, TestName = "Exactly at the edge")]
        [TestCase(4.3f, 4.3f, false, TestName = "Diagonally beyond 6 m")]
        [TestCase(float.NaN, 0f, false, TestName = "Not a number")]
        public void Reach(float x, float z, bool expected) => Assert.AreEqual(expected, WarehouseBounds.IsWithinReach(x, z));

        [Test]
        public void ASavedPlace_IsPlausible_ExactlyWhenTheWarehouseRuleSaysSo()
        {
            foreach (var (x, y, z) in new[] { (1f, 0.13f, 1f), (5.9f, 0.13f, 0f), (6.1f, 0.13f, 0f), (0f, 0f, 0f), (0f, 3.2f, 0f) })
            {
                Assert.AreEqual(WarehouseBounds.IsPlausiblePlace(x, y, z),
                    new BoxPlacement(TestData.Box1, x, y, z, 0f).IsPlausible, $"({x}, {y}, {z})");
            }
        }
    }
}
