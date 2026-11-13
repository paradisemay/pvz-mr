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
            foreach (var step in new[] { "сканер", "номер заказа", "зелёную зону", "пакет", "Возврат", "Поставка", "курок" })
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
            Assert.IsFalse(catalog.Entries[0].recolor, "the first theme shows the counter's own materials");
            foreach (var entry in catalog.Entries)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.displayName), $"{entry.id} has a name");
                if (entry.recolor) Assert.Greater(entry.body.a, 0f, $"{entry.id} has colours");
            }
            Assert.GreaterOrEqual(catalog.Entries.Count(e => e.recolor), 3, "something to unlock");
        }
    }
}
