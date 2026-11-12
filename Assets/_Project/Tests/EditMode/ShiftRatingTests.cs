using NUnit.Framework;

namespace Pvz.Core.Tests
{
    public class ShiftRatingTests
    {
        [TestCase(3, 3, 0, ShiftGrade.Excellent)]
        [TestCase(3, 3, 1, ShiftGrade.Good)]
        [TestCase(3, 3, 2, ShiftGrade.Satisfactory)]
        [TestCase(2, 3, 0, ShiftGrade.Satisfactory)]
        [TestCase(1, 3, 0, ShiftGrade.Poor)]
        [TestCase(0, 0, 0, ShiftGrade.Poor)]
        public void Grade(int completed, int total, int wrong, ShiftGrade expected)
        {
            Assert.AreEqual(expected, ShiftRating.Grade(completed, total, wrong));
        }
    }
}
