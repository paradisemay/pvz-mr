using System.Collections.Generic;

namespace Pvz.Core.Tests
{
    internal static class TestData
    {
        public static readonly BoxId Box1 = new BoxId("BOX-001");
        public static readonly BoxId Box2 = new BoxId("BOX-002");
        public static readonly BoxId Box3 = new BoxId("BOX-003");

        /// <summary>The three boxes from the concept document: BOX-001→1435, BOX-002→2781, BOX-003→5812.</summary>
        public static BoxCatalog MvpCatalog() => new BoxCatalog(new[]
        {
            new BoxRecord(Box1, "1435"),
            new BoxRecord(Box2, "2781"),
            new BoxRecord(Box3, "5812"),
        });

        public static Order OrderFor(BoxId box, string number) => new Order(number, new[] { box });
    }

    /// <summary>Returns a scripted sequence of values (wrapping), clamped into the requested range.</summary>
    internal sealed class FakeRandom : IRandomSource
    {
        private readonly Queue<int> _values;

        public FakeRandom(params int[] values) => _values = new Queue<int>(values);

        public int Range(int minInclusive, int maxExclusive)
        {
            var value = _values.Count > 0 ? _values.Dequeue() : minInclusive;
            if (value < minInclusive) return minInclusive;
            if (value >= maxExclusive) return maxExclusive - 1;
            return value;
        }
    }
}
