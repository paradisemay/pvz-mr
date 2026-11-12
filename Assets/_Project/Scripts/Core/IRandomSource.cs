using System;

namespace Pvz.Core
{
    /// <summary>Randomness abstraction so game rules stay deterministic in tests.</summary>
    public interface IRandomSource
    {
        /// <summary>Returns an integer in [minInclusive, maxExclusive).</summary>
        int Range(int minInclusive, int maxExclusive);
    }

    /// <summary>Default <see cref="IRandomSource"/> backed by <see cref="System.Random"/>.</summary>
    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly Random _random;

        public SystemRandomSource() : this(new Random()) { }
        public SystemRandomSource(int seed) : this(new Random(seed)) { }
        private SystemRandomSource(Random random) => _random = random;

        public int Range(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
    }
}
