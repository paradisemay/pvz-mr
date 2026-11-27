using System;
using System.Collections.Generic;
using Pvz.Core;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// The themes of the pickup point (T022, T048 #87): id and threshold (handed to Core), the name the player sees, the
    /// brand on the sign and the colours of the counter, the board, the computer and the rack. One asset for the whole game — the career spans all difficulties, so it is not in <see cref="ShiftSettings"/>.
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
            [Tooltip("T048: the name of the pickup point in this theme — on the counter's sign and the computer's title.")]
            public string brand = "ПВЗ";
            [Tooltip("T048: the computer's case.")]
            public Color computer = new Color(0.86f, 0.87f, 0.84f);
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
