using System.Linq;
using NUnit.Framework;
using Pvz.Core;
using Pvz.Runtime;
using UnityEditor;

namespace Pvz.Editor.Tests
{
    /// <summary>T022: how the career is shown, and the theme catalog the game ships with.</summary>
    public class CareerTextTests
    {
        [Test]
        public void Menu_ShowsHowFarTheNextThemeIs()
        {
            Assert.AreEqual("Карьера: 400 · до темы «Мята»: 600", ShiftPresenter.CareerText(400, "Мята", 1000));
            Assert.AreEqual("Карьера: 10400 · все темы открыты", ShiftPresenter.CareerText(10400, null, 0));
        }

        [Test]
        public void Summary_CelebratesAnOpenedTheme()
        {
            Assert.AreEqual("Карьера: 600 (+600)", ShiftPresenter.CareerLine(600, 600, new string[0]));
            StringAssert.Contains("Открыта тема «Мята»!", ShiftPresenter.CareerLine(1100, 500, new[] { "Мята" }));
        }

        [Test]
        public void HowToPlay_ExplainsTheLoop()
        {
            var text = ShiftPresenter.HowToPlayText();
            StringAssert.StartsWith("<b>КАК ИГРАТЬ</b>", text);
            foreach (var step in new[] { "сканер", "телефон", "зелёную зону", "пакет", "Возврат", "Поставка", "курок" })
                StringAssert.Contains(step, text);
        }

        [Test]
        public void ThemeButton_NamesTheTheme() => Assert.AreEqual("ТЕМА:\nНОЧЬ", ShiftPresenter.ThemeLabel("Ночь"));

        [Test]
        public void TheShippedCatalog_IsComplete()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CounterThemeCatalog>(PvzSceneBuilder.ThemeCatalogPath);
            Assert.IsNotNull(catalog, "the scene builder creates the catalog");
            Assert.DoesNotThrow(() => new Career(new MemoryScoreStore(), new MemoryTextStore(), catalog.ToCoreThemes()),
                "first theme open, thresholds in order, ids unique");
            // T048 (#87): «Азот», «Разборы», «Рынок» — open from the start, each with its colours and its name on the sign.
            CollectionAssert.AreEqual(new[] { "Азот", "Разборы", "Рынок" }, catalog.Entries.Select(e => e.displayName));
            foreach (var entry in catalog.Entries)
            {
                Assert.AreEqual(0, entry.unlockPoints, $"{entry.id} is open from the start");
                Assert.IsTrue(entry.recolor, $"{entry.id} paints the pickup point");
                Assert.Greater(entry.body.a, 0f, $"{entry.id} has colours");
                Assert.AreEqual(entry.displayName, entry.brand, $"{entry.id} names the pickup point");
            }
        }
    }
}
