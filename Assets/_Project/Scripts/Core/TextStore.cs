using System.Collections.Generic;

namespace Pvz.Core
{
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
}
