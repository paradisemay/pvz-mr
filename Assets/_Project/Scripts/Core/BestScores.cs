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

    /// <summary>Outcome of submitting a finished shift.</summary>
    public readonly struct RecordResult
    {
        public RecordResult(int score, int previousBest, bool isRecord)
        {
            Score = score;
            PreviousBest = previousBest;
            IsRecord = isRecord;
        }

        public int Score { get; }
        /// <summary>Best score before this shift (0 if the difficulty was never played).</summary>
        public int PreviousBest { get; }
        public bool IsRecord { get; }
    }

    /// <summary>
    /// Best shift score per difficulty. Records are keyed by a stable difficulty id (not the display name),
    /// an unplayed difficulty counts as 0, and only a strictly higher score is a new record.
    /// </summary>
    public sealed class BestScores
    {
        private const string KeyPrefix = "pvz.best.";
        private readonly IScoreStore _store;

        public BestScores(IScoreStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public int Get(string difficultyId) => _store.TryGet(Key(difficultyId), out var value) ? value : 0;

        public RecordResult Submit(string difficultyId, int score)
        {
            var best = Get(difficultyId);
            var isRecord = score > best;
            if (isRecord) _store.Set(Key(difficultyId), score);
            return new RecordResult(score, best, isRecord);
        }

        /// <summary>Storage key; stable across runs, independent of letter case and spaces around the id.</summary>
        public static string Key(string difficultyId)
        {
            if (string.IsNullOrWhiteSpace(difficultyId)) throw new ArgumentException("Difficulty must have an id.", nameof(difficultyId));
            return KeyPrefix + difficultyId.Trim().ToLowerInvariant();
        }
    }
}
