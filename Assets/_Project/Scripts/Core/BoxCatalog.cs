using System;
using System.Collections.Generic;

namespace Pvz.Core
{
    /// <summary>
    /// Boxes currently stored in the pickup point (the "warehouse" that is the player's room).
    /// Boxes leave the catalog when they are handed over to a customer.
    /// </summary>
    public sealed class BoxCatalog
    {
        private readonly Dictionary<BoxId, BoxRecord> _byId = new Dictionary<BoxId, BoxRecord>();
        private readonly List<BoxRecord> _ordered = new List<BoxRecord>();

        public BoxCatalog()
        {
            Boxes = _ordered.AsReadOnly();
        }

        public BoxCatalog(IEnumerable<BoxRecord> boxes) : this()
        {
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));
            foreach (var box in boxes) Add(box);
        }

        public int Count => _ordered.Count;

        /// <summary>Boxes in insertion order.</summary>
        public IReadOnlyList<BoxRecord> Boxes { get; }

        public void Add(BoxRecord box)
        {
            if (box == null) throw new ArgumentNullException(nameof(box));
            if (_byId.ContainsKey(box.Id))
                throw new InvalidOperationException($"Box {box.Id} is already in the catalog.");
            _byId.Add(box.Id, box);
            _ordered.Add(box);
        }

        public bool Contains(BoxId id) => _byId.ContainsKey(id);

        public bool TryGet(BoxId id, out BoxRecord box) => _byId.TryGetValue(id, out box);

        public bool Remove(BoxId id)
        {
            if (!_byId.TryGetValue(id, out var box)) return false;
            _byId.Remove(id);
            _ordered.Remove(box);
            return true;
        }
    }
}
