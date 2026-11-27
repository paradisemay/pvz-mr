using System;

namespace Pvz.Core
{
    /// <summary>
    /// T044 (#84): customers come by the clock, not when the line has room — a slow player sees the line grow.
    /// The first one comes after <see cref="FirstDelay"/>, then one every <see cref="Interval"/> (± <see cref="Jitter"/>
    /// of it, so arrivals do not tick like a metronome). When the counter and the line are both empty, the next one
    /// does not keep a quick player waiting a whole interval: they come after <see cref="IdleDelay"/>.
    /// </summary>
    public sealed class ArrivalClock
    {
        private readonly IRandomSource _random;
        private double _next;

        public ArrivalClock(double start, double firstDelay, double interval, double idleDelay, double jitter, IRandomSource random)
        {
            if (interval <= 0) throw new ArgumentOutOfRangeException(nameof(interval), "Customers must come at some pace.");
            FirstDelay = Math.Max(0, firstDelay);
            Interval = interval;
            IdleDelay = Math.Max(0, idleDelay);
            Jitter = Math.Max(0, Math.Min(0.9, jitter));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _next = start + FirstDelay;
        }

        public double FirstDelay { get; }
        public double Interval { get; }
        public double IdleDelay { get; }
        public double Jitter { get; }

        /// <summary>When the next customer comes if the counter stays busy.</summary>
        public double NextArrival => _next;

        /// <summary>
        /// A customer is due now: by the clock, or because nobody has been at the counter or in line since
        /// <paramref name="idleSince"/> for <see cref="IdleDelay"/> (null — somebody is there).
        /// </summary>
        public bool IsDue(double now, double? idleSince) =>
            now >= _next || (idleSince.HasValue && now >= idleSince.Value + IdleDelay);

        /// <summary>A customer came (or was turned away by a full line) at <paramref name="now"/>: the next one is scheduled.</summary>
        public void Arrived(double now)
        {
            // ± Jitter of the interval, in whole percent steps of the random source.
            var percent = Jitter > 0 ? _random.Range(-100, 101) / 100.0 : 0;
            _next = now + Interval * (1 + Jitter * percent);
        }
    }
}
