using System;
using System.Globalization;

namespace Pvz.Core
{
    /// <summary>
    /// Where the counter stands in the room (T034, #78): position on the floor and the turn around the vertical, in the
    /// headset's tracking space (the room). Kept between sessions as text.
    /// </summary>
    public readonly struct CounterPlacement : IEquatable<CounterPlacement>
    {
        public CounterPlacement(float x, float z, float yaw)
        {
            if (float.IsNaN(x) || float.IsNaN(z) || float.IsNaN(yaw) || float.IsInfinity(x) || float.IsInfinity(z) || float.IsInfinity(yaw))
                throw new ArgumentOutOfRangeException(nameof(x), "Expected finite numbers.");
            X = x;
            Z = z;
            Yaw = Normalize(yaw);
        }

        public float X { get; }
        public float Z { get; }
        /// <summary>Degrees, 0..360.</summary>
        public float Yaw { get; }

        private static float Normalize(float degrees)
        {
            var yaw = degrees % 360f;
            return yaw < 0f ? yaw + 360f : yaw;
        }

        /// <summary>«x;z;yaw» with a decimal point.</summary>
        public string Format() => string.Join(";",
            X.ToString("R", CultureInfo.InvariantCulture), Z.ToString("R", CultureInfo.InvariantCulture),
            Yaw.ToString("R", CultureInfo.InvariantCulture));

        /// <summary>Reads <see cref="Format"/>; anything else (nothing saved, a damaged value) is no placement.</summary>
        public static bool TryParse(string text, out CounterPlacement placement)
        {
            placement = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var parts = text.Split(';');
            if (parts.Length != 3) return false;
            var values = new float[3];
            for (var i = 0; i < 3; i++)
            {
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])) return false;
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i])) return false;
            }
            placement = new CounterPlacement(values[0], values[1], values[2]);
            return true;
        }

        public bool Equals(CounterPlacement other) => X.Equals(other.X) && Z.Equals(other.Z) && Yaw.Equals(other.Yaw);
        public override bool Equals(object obj) => obj is CounterPlacement other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Z, Yaw);
        public override string ToString() => Format();
    }

    /// <summary>Saves and loads the counter placement (one key in a text store).</summary>
    public sealed class CounterPlacementStore
    {
        public const string Key = "pvz.counterPlacement";
        private readonly ITextStore _store;

        public CounterPlacementStore(ITextStore store) => _store = store ?? throw new ArgumentNullException(nameof(store));

        public bool TryLoad(out CounterPlacement placement) => CounterPlacement.TryParse(_store.Load(Key), out placement);

        public void Save(CounterPlacement placement) => _store.Save(Key, placement.Format());

        public void Forget() => _store.Save(Key, string.Empty);
    }
}
