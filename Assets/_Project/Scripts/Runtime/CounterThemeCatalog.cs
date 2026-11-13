using System;
using System.Collections.Generic;
using Pvz.Core;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// The counter themes of the career (T022): id and threshold (handed to Core), the name the player sees and the
    /// colours. One asset for the whole game — the career spans all difficulties, so it is not in <see cref="ShiftSettings"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "PVZ/Counter Theme Catalog", fileName = "CounterThemes")]
    public sealed class CounterThemeCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string id = "classic";
            public string displayName = "Классика";
            [Min(0)] public int unlockPoints;
            [Tooltip("Off: the counter keeps its own materials (the first, classic theme).")]
            public bool recolor;
            public Color body = Color.gray;
            public Color top = Color.white;
            public Color accent = Color.yellow;
        }

        [SerializeField] private Entry[] themes = Array.Empty<Entry>();

        public IReadOnlyList<Entry> Entries => themes;

        /// <summary>What Core needs: ids and thresholds, in unlock order.</summary>
        public IReadOnlyList<CounterTheme> ToCoreThemes()
        {
            var list = new List<CounterTheme>();
            foreach (var entry in themes) list.Add(new CounterTheme(entry.id, entry.unlockPoints));
            return list;
        }

        public Entry Find(string id)
        {
            foreach (var entry in themes) if (entry.id == id) return entry;
            return null;
        }

        public string NameOf(CounterTheme theme) => (theme != null ? Find(theme.Id)?.displayName : null) ?? theme?.Id ?? string.Empty;

        internal void Configure(Entry[] entries) => themes = entries;
    }
}
