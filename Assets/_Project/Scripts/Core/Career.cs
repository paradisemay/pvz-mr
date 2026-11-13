using System;
using System.Collections.Generic;

namespace Pvz.Core
{
    /// <summary>
    /// A look for the pickup point opened by career points (concept: «кастомизация ПВЗ», «разблокировки»). Core knows
    /// only the stable id and the threshold; names and colours belong to the game's theme catalog.
    /// </summary>
    public sealed class CounterTheme
    {
        public CounterTheme(string id, int unlockPoints)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Theme must have an id.", nameof(id));
            if (unlockPoints < 0) throw new ArgumentOutOfRangeException(nameof(unlockPoints));
            Id = id.Trim();
            UnlockPoints = unlockPoints;
        }

        /// <summary>Stable key: the player's choice is saved by it.</summary>
        public string Id { get; }
        public int UnlockPoints { get; }

        public override string ToString() => $"{Id} ({UnlockPoints})";
    }

    /// <summary>What finishing a shift changed in the career.</summary>
    public readonly struct CareerResult
    {
        public CareerResult(int added, int total, IReadOnlyList<CounterTheme> unlocked)
        {
            Added = added;
            Total = total;
            Unlocked = unlocked;
        }

        public int Added { get; }
        public int Total { get; }

        /// <summary>Themes this shift opened (usually none).</summary>
        public IReadOnlyList<CounterTheme> Unlocked { get; }
    }

    /// <summary>
    /// The player's career across shifts (concept: «Статистика и прогрессия: … открывать новые улучшения для ПВЗ»):
    /// points of finished shifts add up and open counter themes; the chosen theme is remembered by its id.
    /// </summary>
    public sealed class Career
    {
        public const string PointsKey = "pvz.career.points";
        public const string ThemeKey = "pvz.career.theme";

        private readonly IScoreStore _points;
        private readonly ITextStore _choice;

        /// <param name="points">Where career points are kept (the same int store as best scores, own key prefix).</param>
        /// <param name="choice">Where the chosen theme id is kept.</param>
        /// <param name="themes">Themes in unlock order: the first one is open from the start (threshold 0), thresholds
        /// grow, ids are unique.</param>
        public Career(IScoreStore points, ITextStore choice, IReadOnlyList<CounterTheme> themes)
        {
            _points = points ?? throw new ArgumentNullException(nameof(points));
            _choice = choice ?? throw new ArgumentNullException(nameof(choice));
            Themes = Validate(themes);
        }

        public IReadOnlyList<CounterTheme> Themes { get; }

        public int Points => _points.TryGet(PointsKey, out var points) ? Math.Max(0, points) : 0;

        public bool IsUnlocked(CounterTheme theme) => theme != null && Points >= theme.UnlockPoints;

        /// <summary>The next theme still to open, or null when everything is open.</summary>
        public CounterTheme NextLocked
        {
            get
            {
                foreach (var theme in Themes) if (!IsUnlocked(theme)) return theme;
                return null;
            }
        }

        /// <summary>The theme in use: the saved choice if it exists and is open, otherwise the first theme.</summary>
        public CounterTheme Current
        {
            get
            {
                var saved = _choice.Load(ThemeKey);
                foreach (var theme in Themes)
                {
                    if (theme.Id == saved && IsUnlocked(theme)) return theme;
                }
                return Themes[0];
            }
        }

        /// <summary>
        /// A finished shift adds its points (never negative), weighted by the difficulty: <paramref name="percent"/> of
        /// the score (an easy shift counts less, a hard one more). May open themes.
        /// </summary>
        public CareerResult AddShift(int score, int percent = 100)
        {
            var before = Points;
            var added = (int)Math.Min(int.MaxValue, Math.Max(0L, (long)score * Math.Max(0, percent) / 100));
            var total = before > int.MaxValue - added ? int.MaxValue : before + added;
            if (added > 0) _points.Set(PointsKey, total);
            var unlocked = new List<CounterTheme>();
            foreach (var theme in Themes)
            {
                if (theme.UnlockPoints > before && theme.UnlockPoints <= total) unlocked.Add(theme);
            }
            return new CareerResult(added, total, unlocked);
        }

        /// <summary>Switches to the next open theme (wrapping around) and remembers it.</summary>
        public CounterTheme NextTheme()
        {
            var start = IndexOf(Current);
            for (var step = 1; step <= Themes.Count; step++)
            {
                var theme = Themes[(start + step) % Themes.Count];
                if (!IsUnlocked(theme)) continue;
                _choice.Save(ThemeKey, theme.Id);
                return theme;
            }
            return Current;
        }

        private int IndexOf(CounterTheme theme)
        {
            for (var i = 0; i < Themes.Count; i++) if (Themes[i] == theme) return i;
            return 0;
        }

        private static IReadOnlyList<CounterTheme> Validate(IReadOnlyList<CounterTheme> themes)
        {
            if (themes == null || themes.Count == 0) throw new ArgumentException("A career needs at least one theme.", nameof(themes));
            if (themes[0].UnlockPoints != 0) throw new ArgumentException("The first theme must be open from the start.", nameof(themes));
            var ids = new HashSet<string>();
            for (var i = 0; i < themes.Count; i++)
            {
                if (themes[i] == null) throw new ArgumentException("A theme is missing.", nameof(themes));
                if (!ids.Add(themes[i].Id)) throw new ArgumentException($"Theme id {themes[i].Id} is repeated.", nameof(themes));
                if (i > 0 && themes[i].UnlockPoints < themes[i - 1].UnlockPoints)
                    throw new ArgumentException("Themes must be in unlock order.", nameof(themes));
            }
            return themes;
        }
    }
}
