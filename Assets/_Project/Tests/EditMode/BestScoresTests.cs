using NUnit.Framework;

namespace Pvz.Core.Tests
{
    public class BestScoresTests
    {
        private BestScores _best;

        [SetUp]
        public void SetUp() => _best = new BestScores(new MemoryScoreStore());

        [Test]
        public void UnplayedDifficulty_CountsAsZero_AndZeroIsNoRecord()
        {
            Assert.AreEqual(0, _best.Get("normal"));
            var result = _best.Submit("normal", 0);
            Assert.IsFalse(result.IsRecord, "a shift where everyone left is not a record");
        }

        [Test]
        public void FirstPositiveResult_IsARecord()
        {
            var result = _best.Submit("normal", 150);
            Assert.IsTrue(result.IsRecord);
            Assert.AreEqual(0, result.PreviousBest);
            Assert.AreEqual(150, _best.Get("normal"));
        }

        [Test]
        public void OnlyAHigherScore_BeatsTheRecord()
        {
            _best.Submit("normal", 500);
            Assert.IsFalse(_best.Submit("normal", 400).IsRecord);
            Assert.IsFalse(_best.Submit("normal", 500).IsRecord);
            var beat = _best.Submit("normal", 501);
            Assert.IsTrue(beat.IsRecord);
            Assert.AreEqual(500, beat.PreviousBest);
            Assert.AreEqual(501, _best.Get("normal"));
        }

        [Test]
        public void Difficulties_AreIndependent()
        {
            _best.Submit("easy", 900);
            Assert.AreEqual(0, _best.Get("hard"));
            Assert.IsTrue(_best.Submit("hard", 100).IsRecord);
        }

        [Test]
        public void Keys_AreStable()
        {
            Assert.AreEqual(BestScores.Key("Hard"), BestScores.Key("  hard "));
            Assert.AreEqual("pvz.best.normal", BestScores.Key("normal"));
        }

        [Test]
        public void Records_SurviveANewInstance_OnTheSameStore()
        {
            var store = new MemoryScoreStore();
            new BestScores(store).Submit("normal", 321);
            Assert.AreEqual(321, new BestScores(store).Get("normal"));
        }
    }
}
