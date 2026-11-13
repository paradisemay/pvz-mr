using System;
using Pvz.Core;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// The player's career between shifts (T022): owns <see cref="Career"/>, paints the counter in the chosen theme,
    /// serves the «ТЕМА» button. Not part of a shift — the director only reports a finished shift's score.
    /// </summary>
    public sealed class CareerKeeper : MonoBehaviour
    {
        [SerializeField] private CounterThemeCatalog catalog;
        [SerializeField] private CounterSkin skin;
        [SerializeField] private ThemeButton themeButton;

        /// <summary>Tests set this before the scene loads, so the career never touches the real saved data. Null in the game.</summary>
        internal static (IScoreStore points, ITextStore choice)? StoreOverride { get; set; }

        public Career Career { get; private set; }
        public CounterThemeCatalog Catalog => catalog;
        public CounterSkin Skin => skin;

        private void Awake()
        {
            var stores = StoreOverride ?? (new PlayerPrefsScoreStore(), new PlayerPrefsTextStore());
            try { Career = new Career(stores.points, stores.choice, catalog.ToCoreThemes()); }
            catch (Exception e)
            {
                Debug.LogException(e, this); // a broken catalog: no career, the game itself still works
                enabled = false;
            }
        }

        private void Start() => ShowTheme();

        /// <summary>«ТЕМА»: the next theme the career has opened.</summary>
        public void NextTheme()
        {
            if (!isActiveAndEnabled || Career == null) return;
            Career.NextTheme();
            ShowTheme();
        }

        /// <summary>A finished shift adds its points (weighted by the difficulty). Null without a career.</summary>
        public CareerResult? AddShift(int score, int percent)
        {
            if (!isActiveAndEnabled || Career == null) return null;
            return Career.AddShift(score, percent);
        }

        /// <summary>The career line of the start menu.</summary>
        public string MenuText()
        {
            if (Career == null) return string.Empty;
            var next = Career.NextLocked;
            return ShiftPresenter.CareerText(Career.Points, next != null ? catalog.NameOf(next) : null,
                next?.UnlockPoints ?? 0);
        }

        /// <summary>The career line of the shift summary.</summary>
        public string SummaryText(CareerResult result)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (var theme in result.Unlocked) names.Add(catalog.NameOf(theme));
            return ShiftPresenter.CareerLine(result.Total, result.Added, names);
        }

        private void ShowTheme()
        {
            if (Career == null) return;
            var theme = Career.Current;
            if (skin != null) skin.Apply(catalog.Find(theme.Id));
            if (themeButton != null) themeButton.ShowLabel(ShiftPresenter.ThemeLabel(catalog.NameOf(theme)));
        }

        internal void Configure(CounterThemeCatalog themes, CounterSkin counterSkin, ThemeButton button)
        {
            catalog = themes;
            skin = counterSkin;
            themeButton = button;
        }
    }
}
