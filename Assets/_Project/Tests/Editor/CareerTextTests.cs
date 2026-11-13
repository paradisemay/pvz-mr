using NUnit.Framework;
using Pvz.Core;
using Pvz.Runtime;

namespace Pvz.Editor.Tests
{
    /// <summary>T022: how the career is shown.</summary>
    public class CareerTextTests
    {
        [Test]
        public void Menu_ShowsHowFarTheNextThemeIs()
        {
            var store = new MemoryScoreStore();
            var career = new Career(store);
            career.AddShift(400);
            Assert.AreEqual("Карьера: 400 · до темы «Мята»: 600", ShiftPresenter.CareerText(career));
            career.AddShift(10000);
            Assert.AreEqual("Карьера: 10400 · все темы открыты", ShiftPresenter.CareerText(career));
        }

        [Test]
        public void Summary_CelebratesAnOpenedTheme()
        {
            var career = new Career(new MemoryScoreStore());
            Assert.AreEqual("Карьера: 600 (+600)", ShiftPresenter.CareerLine(career.AddShift(600)));
            StringAssert.Contains("Открыта тема «Мята»!", ShiftPresenter.CareerLine(career.AddShift(500)));
        }

        [Test]
        public void ThemeButton_NamesTheTheme() =>
            Assert.AreEqual("ТЕМА:\nНОЧЬ", ShiftPresenter.ThemeLabel(Career.Themes[2]));
    }
}
