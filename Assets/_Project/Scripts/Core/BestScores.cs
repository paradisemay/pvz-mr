using System;
using System.Collections.Generic;

namespace Pvz.Core
{
    /// <summary>Persistent storage of integers by key (PlayerPrefs on the device, a dictionary in tests).</summary>
    public interface IScoreStore
    {
        bool TryGet(string key, out int value);
        void Set(string key, int value);
    }

    /// <summary>In-memory <see cref="IScoreStore"/> (tests, or when nothing should persist).</summary>
    public sealed class MemoryScoreStore : IScoreStore
    {
        private readonly Dictionary<string, int> _values = new Dictionary<string, int>();

        public bool TryGet(string key, out int value) => _values.TryGetValue(key, out value);
        public void Set(string key, int value) => _values[key] = value;
    }

    /// <summary>Best shift score per difficulty.</summary>
    public sealed class BestScores
    {
        private const string KeyPrefix = "pvz.best.";
        private readonly IScoreStore _store;

        public BestScores(IScoreStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>Best score for the difficulty, or null if it was never played.</summary>
        public int? Get(string difficulty) => _store.TryGet(Key(difficulty), out var value) ? value : (int?)null;

        /// <summary>Records a finished shift; returns true when it beats (or sets) the record.</summary>
        public bool Submit(string difficulty, int score)
        {
            var best = Get(difficulty);
            if (best.HasValue && score <= best.Value) return false;
            _store.Set(Key(difficulty), score);
            return true;
        }

        /// <summary>Storage key; stable across runs, independent of letter case and spaces around the name.</summary>
        public static string Key(string difficulty)
        {
            if (string.IsNullOrWhiteSpace(difficulty)) throw new ArgumentException("Difficulty must have a name.", nameof(difficulty));
            return KeyPrefix + difficulty.Trim().ToLowerInvariant();
        }
    }
}
