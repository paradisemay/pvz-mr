using NUnit.Framework;
using Pvz.Runtime;
using UnityEngine;

namespace Pvz.Editor.Tests
{
    /// <summary>T034 (#78): which way a held handle points on the floor.</summary>
    public class CounterHandleYawTests
    {
        [Test]
        public void Level_Handle_UsesWhereItPoints()
        {
            Assert.AreEqual(90f, CounterMover.YawOf(Vector3.right, Vector3.up, 0f), 0.01f);
            Assert.AreEqual(90f, CounterMover.YawOf(new Vector3(1f, -0.5f, 0f), Vector3.up, 0f), 0.01f, "tilt does not matter");
        }

        [Test]
        public void PointingStraightDown_UsesWhereItsTopPoints_NotTheWristRoll()
        {
            // Pointing down with its top towards +X: the heading is +X (90°), steady.
            Assert.AreEqual(90f, CounterMover.YawOf(Vector3.down, Vector3.right, 0f), 0.01f);
            Assert.AreEqual(90f, CounterMover.YawOf(new Vector3(0.01f, -1f, 0.005f), Vector3.right, 0f), 1f);
        }

        [Test]
        public void NoDirection_KeepsTheFallback()
        {
            Assert.AreEqual(33f, CounterMover.YawOf(Vector3.up, Vector3.up, 33f), 0.01f);
        }
    }
}
