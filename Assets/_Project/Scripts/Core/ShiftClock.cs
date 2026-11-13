using System;

namespace Pvz.Core
{
    /// <summary>
    /// How long the shift lasts. Knows only time; what happens when it runs out is decided by <see cref="Shift"/>.
    /// Time is always passed in (<c>now</c>), so the clock is deterministic in tests.
    /// </summary>
    public sealed class ShiftClock
    {
        private double _limitSeconds;

        /// <summary>Length of the shift, seconds; 0 = no time limit.</summary>
        public double LimitSeconds
        {
            get => _limitSeconds;
            set
            {
                if (value < 0 || double.IsNaN(value)) throw new ArgumentOutOfRangeException(nameof(value), value, "Expected 0 (no limit) or more.");
                // Changing the limit of a running shift is a test hook only; settings set it before the start.
                _limitSeconds = value;
            }
        }

        /// <summary>When the shift started, or null before <see cref="Start"/>.</summary>
        public double? StartedAt { get; private set; }

        /// <summary>The time ran out (stays true).</summary>
        public bool HasRunOut { get; private set; }

        /// <summary>Starts the clock; only the first call counts.</summary>
        public void Start(double now)
        {
            if (StartedAt == null) StartedAt = now;
        }

        /// <summary>Seconds left, or null when there is no limit or the clock has not started.</summary>
        public double? TimeLeft(double now) => _limitSeconds <= 0 || StartedAt == null
            ? (double?)null
            : Math.Max(0.0, StartedAt.Value + _limitSeconds - now);

        /// <summary>True exactly once: on the first call after the time ran out.</summary>
        public bool TryRunOut(double now)
        {
            if (HasRunOut || TimeLeft(now) != 0) return false;
            HasRunOut = true;
            return true;
        }
    }
}
