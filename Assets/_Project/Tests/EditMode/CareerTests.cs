using System.Linq;
using NUnit.Framework;

namespace Pvz.Core.Tests
{
    /// <summary>T022: career points across shifts open counter themes.</summary>
    public class CareerTests
    {
        [Test]
        public void ANewPlayer_HasOnlyTheClassicTheme()
        {
            var career = new Career(new MemoryScoreStore());
            Assert.AreEqual(0, career.Points);
            Assert.AreEqual("classic", career.Current.Id);
            Assert.AreEqual("mint", career.NextLocked.Id);
            Assert.AreEqual("classic", career.NextTheme().Id, "nothing else to switch to");
        }

        [Test]
        public void ShiftPoints_AddUp_AndOpenThemes_Once()
        {
            var career = new Career(new MemoryScoreStore());
            var first = career.AddShift(600);
            Assert.AreEqual(600, first.Total);
            Assert.IsEmpty(first.Unlocked);

            var second = career.AddShift(500);
            Assert.AreEqual(1100, second.Total);
            CollectionAssert.AreEqual(new[] { "mint" }, second.Unlocked.Select(t => t.Id));

            Assert.IsEmpty(career.AddShift(100).Unlocked, "an open theme is not opened again");
        }

        [Test]
        public void ABigShift_CanOpenSeveralThemes()
        {
            var career = new Career(new MemoryScoreStore());
            CollectionAssert.AreEqual(new[] { "mint", "night" }, career.AddShift(2600).Unlocked.Select(t => t.Id));
        }

        [Test]
        public void ZeroOrNegativeScores_ChangeNothing()
        {
            var store = new MemoryScoreStore();
            var career = new Career(store);
            career.AddShift(0);
            career.AddShift(-50);
            Assert.AreEqual(0, career.Points);
            Assert.IsFalse(store.TryGet(Career.PointsKey, out _), "nothing written");
        }

        [Test]
        public void TheChosenTheme_IsRemembered_AndCyclesThroughOpenOnes()
        {
            var store = new MemoryScoreStore();
            var career = new Career(store);
            career.AddShift(2600);
            Assert.AreEqual("mint", career.NextTheme().Id);
            Assert.AreEqual("night", career.NextTheme().Id);
            Assert.AreEqual("classic", career.NextTheme().Id, "gold is still closed: back to the start");
            career.NextTheme();
            Assert.AreEqual("mint", new Career(store).Current.Id, "remembered between sessions");
        }

        [Test]
        public void ASavedThemeThatIsNotOpen_FallsBackToClassic()
        {
            var store = new MemoryScoreStore();
            store.Set(Career.ThemeKey, 3); // gold, but no points (damaged or edited data)
            Assert.AreEqual("classic", new Career(store).Current.Id);
            store.Set(Career.ThemeKey, 99);
            Assert.AreEqual("classic", new Career(store).Current.Id);
        }

        [Test]
        public void Points_DoNotOverflow()
        {
            var store = new MemoryScoreStore();
            store.Set(Career.PointsKey, int.MaxValue - 10);
            Assert.AreEqual(int.MaxValue, new Career(store).AddShift(100).Total);
        }
    }
}
