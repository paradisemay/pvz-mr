using System;
using System.Collections.Generic;

namespace Pvz.Core
{
    /// <summary>A look for the pickup point, opened by career points (concept: «кастомизация ПВЗ», «разблокировки»).</summary>
    public sealed class CounterTheme
    {
        public CounterTheme(string id, string name, int unlockPoints)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Theme must have an id.", nameof(id));
            if (unlockPoints < 0) throw new ArgumentOutOfRangeException(nameof(unlockPoints));
            Id = id;
            Name = name;
            UnlockPoints = unlockPoints;
        }

        /// <summary>Stable key (saved); the colours of a theme live in the game, not here.</summary>
        public string Id { get; }
        public string Name { get; }
        public int UnlockPoints { get; }

        public override string ToString() => $"{Name} ({UnlockPoints})";
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
    /// points of every finished shift add up and open counter themes; the chosen theme is remembered.
    /// </summary>
    public sealed class Career
    {
        public const string PointsKey = "pvz.career.points";
        public const string ThemeKey = "pvz.career.theme";

        /// <summary>The themes in unlock order; the first one is open from the start.</summary>
        public static readonly IReadOnlyList<CounterTheme> Themes = new[]
        {
            new CounterTheme("classic", "Классика", 0),
            new CounterTheme("mint", "Мята", 1000),
            new CounterTheme("night", "Ночь", 2500),
            new CounterTheme("gold", "Золото", 5000),
        };

        private readonly IScoreStore _store;

        public Career(IScoreStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public int Points => _store.TryGet(PointsKey, out var points) ? Math.Max(0, points) : 0;

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

        /// <summary>The theme in use: the saved choice if it is (still) open, otherwise the first theme.</summary>
        public CounterTheme Current
        {
            get
            {
                var index = _store.TryGet(ThemeKey, out var saved) ? saved : 0;
                return index >= 0 && index < Themes.Count && IsUnlocked(Themes[index]) ? Themes[index] : Themes[0];
            }
        }

        /// <summary>A finished shift adds its points (never negative) and may open themes.</summary>
        public CareerResult AddShift(int score)
        {
            var before = Points;
            var added = Math.Max(0, score);
            var total = before > int.MaxValue - added ? int.MaxValue : before + added;
            if (added > 0) _store.Set(PointsKey, total);
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
                var index = (start + step) % Themes.Count;
                if (!IsUnlocked(Themes[index])) continue;
                _store.Set(ThemeKey, index);
                return Themes[index];
            }
            return Current;
        }

        private static int IndexOf(CounterTheme theme)
        {
            for (var i = 0; i < Themes.Count; i++) if (Themes[i] == theme) return i;
            return 0;
        }
    }
}
