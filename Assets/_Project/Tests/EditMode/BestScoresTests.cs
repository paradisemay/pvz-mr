using NUnit.Framework;

namespace Pvz.Core.Tests
{
    public class BestScoresTests
    {
        private BestScores _best;

        [SetUp]
        public void SetUp() => _best = new BestScores(new MemoryScoreStore());

        [Test]
        public void FirstResult_IsARecord()
        {
            Assert.IsNull(_best.Get("Обычная"));
            Assert.IsTrue(_best.Submit("Обычная", 0));
            Assert.AreEqual(0, _best.Get("Обычная"));
        }

        [Test]
        public void OnlyAHigherScore_BeatsTheRecord()
        {
            _best.Submit("Обычная", 500);
            Assert.IsFalse(_best.Submit("Обычная", 400));
            Assert.IsFalse(_best.Submit("Обычная", 500));
            Assert.IsTrue(_best.Submit("Обычная", 501));
            Assert.AreEqual(501, _best.Get("Обычная"));
        }

        [Test]
        public void Difficulties_AreIndependent()
        {
            _best.Submit("Лёгкая", 900);
            Assert.IsNull(_best.Get("Сложная"));
            Assert.IsTrue(_best.Submit("Сложная", 100));
        }

        [Test]
        public void Keys_AreStable()
        {
            Assert.AreEqual(BestScores.Key("Сложная"), BestScores.Key("  сложная "));
            Assert.AreEqual("pvz.best.обычная", BestScores.Key("Обычная"));
        }

        [Test]
        public void Records_SurviveANewInstance_OnTheSameStore()
        {
            var store = new MemoryScoreStore();
            new BestScores(store).Submit("Обычная", 321);
            Assert.AreEqual(321, new BestScores(store).Get("Обычная"));
        }
    }
}
