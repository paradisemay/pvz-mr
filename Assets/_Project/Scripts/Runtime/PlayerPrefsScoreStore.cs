using Pvz.Core;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>Keeps best scores on the device between sessions.</summary>
    public sealed class PlayerPrefsScoreStore : IScoreStore
    {
        public bool TryGet(string key, out int value)
        {
            if (!PlayerPrefs.HasKey(key))
            {
                value = 0;
                return false;
            }
            value = PlayerPrefs.GetInt(key);
            return true;
        }

        public void Set(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }
    }
}
