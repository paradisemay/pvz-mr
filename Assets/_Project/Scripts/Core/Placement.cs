using System;
using System.Globalization;

namespace Pvz.Core
{
    /// <summary>
    /// Where something stands in the room (the counter T034, the supply hatch T036, the shelf rack T037): position on the
    /// floor and the turn around the vertical, in the headset's tracking space (the room). Kept between sessions as text.
    /// </summary>
    public readonly struct Placement : IEquatable<Placement>
    {
        public Placement(float x, float z, float yaw)
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
        public static bool TryParse(string text, out Placement placement)
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
            placement = new Placement(values[0], values[1], values[2]);
            return true;
        }

        public bool Equals(Placement other) => X.Equals(other.X) && Z.Equals(other.Z) && Yaw.Equals(other.Yaw);
        public override bool Equals(object obj) => obj is Placement other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Z, Yaw);
        public override string ToString() => Format();
    }

    /// <summary>Saves and loads one placement (one key in a text store).</summary>
    public sealed class PlacementStore
    {
        public const string CounterKey = "pvz.counterPlacement";
        public const string HatchKey = "pvz.hatchPlacement";
        public const string RackKey = "pvz.rackPlacement";

        private readonly ITextStore _store;
        private readonly string _key;

        public PlacementStore(ITextStore store, string key = CounterKey)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _key = string.IsNullOrEmpty(key) ? throw new ArgumentException("A key is needed.", nameof(key)) : key;
        }

        public bool TryLoad(out Placement placement) => Placement.TryParse(_store.Load(_key), out placement);

        public void Save(Placement placement) => _store.Save(_key, placement.Format());

        public void Forget() => _store.Save(_key, string.Empty);
    }
}
