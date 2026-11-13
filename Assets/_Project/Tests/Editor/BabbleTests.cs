using NUnit.Framework;
using Pvz.Runtime;

namespace Pvz.Editor.Tests
{
    /// <summary>T018: the customers' procedural voice.</summary>
    public class BabbleTests
    {
        [TestCase("Да", 2, TestName = "Short line: at least two syllables")]
        [TestCase("Здравствуйте!\nЗаказ №2781", 5, TestName = "Greeting")]
        [TestCase("Очень-очень длинная реплика клиента, который никак не может остановиться и всё говорит", 8,
            TestName = "Long line: capped")]
        [TestCase(null, 2, TestName = "No text")]
        public void SyllableCount_FollowsTheLength(string line, int expected) =>
            Assert.AreEqual(expected, Babble.SyllableCount(line));

        [Test]
        public void SameLine_SoundsTheSame_AndLongerLines_Last_Longer()
        {
            var once = Babble.Samples("Здравствуйте!", 180f, 22050);
            CollectionAssert.AreEqual(once, Babble.Samples("Здравствуйте!", 180f, 22050), "deterministic");
            Assert.Greater(Babble.Samples("Здравствуйте! Заказ №2781", 180f, 22050).Length, once.Length);
        }

        [Test]
        public void Samples_StayWithinASafeVolume()
        {
            foreach (var sample in Babble.Samples("Похож, но не мой! Мне нужен №2781", 270f, 22050))
                Assert.LessOrEqual(System.Math.Abs(sample), 0.5f);
        }
    }
}
