using System;
using System.Linq;
using NUnit.Framework;

namespace Pvz.Core.Tests
{
    /// <summary>T022: career points across shifts open counter themes.</summary>
    public class CareerTests
    {
        private static readonly CounterTheme[] Themes =
        {
            new CounterTheme("classic", 0), new CounterTheme("mint", 1000),
            new CounterTheme("night", 2500), new CounterTheme("gold", 5000),
        };

        private static Career NewCareer(MemoryScoreStore points = null, MemoryTextStore choice = null) =>
            new Career(points ?? new MemoryScoreStore(), choice ?? new MemoryTextStore(), Themes);

        [Test]
        public void ANewPlayer_HasOnlyTheFirstTheme()
        {
            var career = NewCareer();
            Assert.AreEqual(0, career.Points);
            Assert.AreEqual("classic", career.Current.Id);
            Assert.AreEqual("mint", career.NextLocked.Id);
            Assert.AreEqual("classic", career.NextTheme().Id, "nothing else to switch to");
        }

        [Test]
        public void ShiftPoints_AddUp_AndOpenThemes_Once()
        {
            var career = NewCareer();
            Assert.IsEmpty(career.AddShift(600).Unlocked);
            var second = career.AddShift(500);
            Assert.AreEqual(1100, second.Total);
            CollectionAssert.AreEqual(new[] { "mint" }, second.Unlocked.Select(t => t.Id));
            Assert.IsEmpty(career.AddShift(100).Unlocked, "an open theme is not opened again");
        }

        [Test]
        public void ABigShift_CanOpenSeveralThemes() =>
            CollectionAssert.AreEqual(new[] { "mint", "night" }, NewCareer().AddShift(2600).Unlocked.Select(t => t.Id));

        [Test]
        public void TheDifficulty_WeighsTheShift()
        {
            var career = NewCareer();
            Assert.AreEqual(300, career.AddShift(600, 50).Added, "an easy shift counts half");
            Assert.AreEqual(600, career.AddShift(600, 100).Added, "a normal shift counts as it is");
            Assert.AreEqual(900, career.Points);
            Assert.AreEqual(150, career.AddShift(100, 150).Added, "a hard shift counts more");
        }

        [Test]
        public void ZeroOrNegativeScores_ChangeNothing()
        {
            var points = new MemoryScoreStore();
            var career = NewCareer(points);
            career.AddShift(0);
            career.AddShift(-50);
            Assert.AreEqual(0, career.Points);
            Assert.IsFalse(points.TryGet(Career.PointsKey, out _), "nothing written");
        }

        [Test]
        public void TheChosenTheme_IsRememberedByItsId_AndCyclesThroughOpenOnes()
        {
            var points = new MemoryScoreStore();
            var choice = new MemoryTextStore();
            var career = NewCareer(points, choice);
            career.AddShift(2600);
            Assert.AreEqual("mint", career.NextTheme().Id);
            Assert.AreEqual("night", career.NextTheme().Id);
            Assert.AreEqual("classic", career.NextTheme().Id, "gold is still closed: back to the start");
            career.NextTheme();
            Assert.AreEqual("mint", choice.Load(Career.ThemeKey), "saved by id, not by position");

            // A new theme inserted before «mint» does not change the player's choice.
            var reordered = new[] { Themes[0], new CounterTheme("sky", 500), Themes[1], Themes[2], Themes[3] };
            Assert.AreEqual("mint", new Career(points, choice, reordered).Current.Id);
        }

        [Test]
        public void ASavedThemeThatIsNotOpen_OrUnknown_FallsBackToTheFirst()
        {
            var choice = new MemoryTextStore();
            choice.Save(Career.ThemeKey, "gold"); // no points (damaged or edited data)
            Assert.AreEqual("classic", NewCareer(choice: choice).Current.Id);
            choice.Save(Career.ThemeKey, "no-such-theme");
            Assert.AreEqual("classic", NewCareer(choice: choice).Current.Id);
        }

        [Test]
        public void Points_DoNotOverflow()
        {
            var points = new MemoryScoreStore();
            points.Set(Career.PointsKey, int.MaxValue - 10);
            Assert.AreEqual(int.MaxValue, NewCareer(points).AddShift(100).Total);
        }

        [Test]
        public void ABrokenThemeList_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => new Career(new MemoryScoreStore(), new MemoryTextStore(), new CounterTheme[0]));
            Assert.Throws<ArgumentException>(() => new Career(new MemoryScoreStore(), new MemoryTextStore(),
                new[] { new CounterTheme("mint", 1000) }), "the first theme must be open");
            Assert.Throws<ArgumentException>(() => new Career(new MemoryScoreStore(), new MemoryTextStore(),
                new[] { Themes[0], new CounterTheme("classic", 10) }), "repeated id");
            Assert.Throws<ArgumentException>(() => new Career(new MemoryScoreStore(), new MemoryTextStore(),
                new[] { Themes[0], Themes[2], Themes[1] }), "out of order");
        }
    }
}
