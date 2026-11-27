using NUnit.Framework;

namespace Pvz.Core.Tests
{
    /// <summary>«Where a box may be» (T025): out of reach a box is lost and drops out of the hatch again (T036).</summary>
    public class WarehouseBoundsTests
    {
        [TestCase(0f, 0f, true)]
        [TestCase(6f, 0f, true, TestName = "Exactly at the edge")]
        [TestCase(4.3f, 4.3f, false, TestName = "Diagonally beyond 6 m")]
        [TestCase(float.NaN, 0f, false, TestName = "Not a number")]
        public void Reach(float x, float z, bool expected) => Assert.AreEqual(expected, WarehouseBounds.IsWithinReach(x, z));

    }
}
