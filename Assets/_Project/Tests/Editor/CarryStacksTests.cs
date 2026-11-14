using NUnit.Framework;
using Pvz.Runtime;

namespace Pvz.Editor.Tests
{
    /// <summary>T027: boxes a customer carries never cover their face.</summary>
    public class CarryStacksTests
    {
        private const float Box = 0.26f;
        private const float Chin = 0.49f; // above the carry anchor
        private const float HeadHalfWidth = 0.13f;

        [Test]
        public void FirstBox_IsHeldAtTheChest()
        {
            Assert.AreEqual(0, CarryStacks.PickColumn(new float[3], Box));
            Assert.AreEqual(0, CarryStacks.PickColumn(new float[3], 0.6f), "even a tall box is held in front");
        }

        [Test]
        public void SecondBox_GoesToTheSide_ThenTheOtherSide()
        {
            var stacks = new float[3];
            var columns = new int[4];
            for (var i = 0; i < columns.Length; i++)
            {
                columns[i] = CarryStacks.PickColumn(stacks, Box);
                stacks[columns[i]] += Box;
            }
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 1 }, columns);
        }

        [Test]
        public void SmallBoxes_ShareTheChestStack_WhileTheyStayBelowTheChin()
        {
            Assert.AreEqual(0, CarryStacks.PickColumn(new[] { 0.2f, 0f, 0f }, 0.2f));
            Assert.AreEqual(1, CarryStacks.PickColumn(new[] { 0.3f, 0f, 0f }, 0.2f));
        }

        [Test]
        public void NothingInFrontOfTheFace_ForOrdersUpToTenBoxes()
        {
            // Sizes as in PvzPrefabBuilder (BoxSize, the head and the carry anchor), which tests cannot reach.
            var box = new UnityEngine.Vector3(0.34f, Box, 0.34f);
            Assert.Less(CarryStacks.FaceClearance, Chin);
            var stacks = new float[3];
            for (var i = 0; i < 10; i++)
            {
                var centre = CarryStacks.Place(stacks, box.y);
                var top = centre.y + box.y / 2f;
                var overlapsHead = System.Math.Abs(centre.x) < HeadHalfWidth + box.x / 2f;
                Assert.IsFalse(overlapsHead && top > Chin, $"box {i + 1} would cover the face");
            }
        }

        [Test]
        public void Place_StacksBoxesOnTopOfEachOther_WithAGap()
        {
            var stacks = new float[3];
            Assert.AreEqual(Box / 2f, CarryStacks.Place(stacks, Box).y, 1e-5);
            var side = CarryStacks.Place(stacks, Box);
            Assert.AreEqual(CarryStacks.ColumnX[1], side.x, 1e-5);
            CarryStacks.Place(stacks, Box); // left
            var secondOnRight = CarryStacks.Place(stacks, Box);
            Assert.AreEqual(Box + CarryStacks.Gap + Box / 2f, secondOnRight.y, 1e-5);
        }

        [Test]
        public void WrongStacks_AreRejected()
        {
            Assert.Throws<System.ArgumentException>(() => CarryStacks.PickColumn(new float[1], Box));
        }
    }
}
