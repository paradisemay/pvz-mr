using Pvz.Core;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>Keeps texts (the warehouse layout) on the device between sessions.</summary>
    public sealed class PlayerPrefsTextStore : ITextStore
    {
        public string Load(string key) => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;

        public void Save(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }
    }
}
