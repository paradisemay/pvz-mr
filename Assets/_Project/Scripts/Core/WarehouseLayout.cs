using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Pvz.Core
{
    /// <summary>Where the player last put a box down, in the play area's coordinates (metres, yaw in degrees).</summary>
    public readonly struct BoxPlacement
    {
        public BoxPlacement(BoxId box, float x, float y, float z, float yaw)
        {
            Box = box;
            X = x;
            Y = y;
            Z = z;
            Yaw = yaw;
        }

        public BoxId Box { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float Yaw { get; }

        /// <summary>Inside the room the game can use (<see cref="WarehouseBounds"/>); damaged data never is.</summary>
        public bool IsPlausible =>
            !Box.IsEmpty && IsFinite(X) && IsFinite(Y) && IsFinite(Z) && IsFinite(Yaw) &&
            WarehouseBounds.IsPlausiblePlace(X, Y, Z);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public override string ToString() => $"{Box} ({X:0.##}, {Y:0.##}, {Z:0.##}) {Yaw:0}°";
    }

    /// <summary>
    /// The player's own arrangement of the warehouse: where each box was last put down. It is kept between sessions,
    /// so the room stays organised the way the player left it (concept: «сохранение расположения склада»).
    /// </summary>
    public sealed class WarehouseLayout
    {
        private const char EntrySeparator = '|';
        private const char FieldSeparator = ';';
        private const string Version = "v1";

        private readonly Dictionary<BoxId, BoxPlacement> _placements = new Dictionary<BoxId, BoxPlacement>();

        public static WarehouseLayout Empty => new WarehouseLayout();

        public int Count => _placements.Count;

        public IEnumerable<BoxPlacement> Placements => _placements.Values;

        /// <summary>Remembers a place; an implausible one (damaged data, a box thrown out of the room) is ignored.</summary>
        public bool Set(BoxPlacement placement)
        {
            if (!placement.IsPlausible) return false;
            _placements[placement.Box] = placement;
            return true;
        }

        public bool TryGet(BoxId box, out BoxPlacement placement) => _placements.TryGetValue(box, out placement);

        /// <summary>«v1|BOX-001;x;y;z;yaw|…» with invariant numbers (decimal point everywhere).</summary>
        public string Encode()
        {
            var text = new StringBuilder(Version);
            foreach (var p in _placements.Values)
            {
                text.Append(EntrySeparator).Append(p.Box.Value).Append(FieldSeparator)
                    .Append(Number(p.X)).Append(FieldSeparator).Append(Number(p.Y)).Append(FieldSeparator)
                    .Append(Number(p.Z)).Append(FieldSeparator).Append(Number(p.Yaw));
            }
            return text.ToString();
        }

        /// <summary>
        /// Reads <see cref="Encode"/>'s text. Never throws: unknown versions, broken entries and implausible places are
        /// skipped, so damaged saved data can only lose the arrangement, not break the game.
        /// </summary>
        public static WarehouseLayout Decode(string text)
        {
            var layout = new WarehouseLayout();
            if (string.IsNullOrEmpty(text)) return layout;
            var entries = text.Split(EntrySeparator);
            if (entries[0] != Version) return layout;
            for (var i = 1; i < entries.Length; i++)
            {
                var fields = entries[i].Split(FieldSeparator);
                if (fields.Length != 5 || string.IsNullOrWhiteSpace(fields[0])) continue;
                if (!TryNumber(fields[1], out var x) || !TryNumber(fields[2], out var y) ||
                    !TryNumber(fields[3], out var z) || !TryNumber(fields[4], out var yaw)) continue;
                layout.Set(new BoxPlacement(new BoxId(fields[0]), x, y, z, yaw));
            }
            return layout;
        }

        private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private static bool TryNumber(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Persistent storage of a text by key (PlayerPrefs on the device, a dictionary in tests).</summary>
    public interface ITextStore
    {
        string Load(string key);
        void Save(string key, string value);
    }

    /// <summary>In-memory <see cref="ITextStore"/> (tests, or when nothing should persist).</summary>
    public sealed class MemoryTextStore : ITextStore
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

        public string Load(string key) => _values.TryGetValue(key, out var value) ? value : null;
        public void Save(string key, string value) => _values[key] = value;
    }

    /// <summary>Loads and saves the <see cref="WarehouseLayout"/>.</summary>
    public sealed class WarehouseLayoutStore
    {
        public const string Key = "pvz.layout";
        private readonly ITextStore _store;

        public WarehouseLayoutStore(ITextStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public WarehouseLayout Load() => WarehouseLayout.Decode(_store.Load(Key));

        public void Save(WarehouseLayout layout) =>
            _store.Save(Key, (layout ?? throw new ArgumentNullException(nameof(layout))).Encode());
    }
}
