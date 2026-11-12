using System.Collections.Generic;
using Pvz.Core;

namespace Pvz.Runtime
{
    /// <summary>Scene boxes by id. Boxes can be added during a shift (e.g. a future supply event).</summary>
    public sealed class BoxRegistry
    {
        private readonly Dictionary<BoxId, QrBox> _boxes = new Dictionary<BoxId, QrBox>();

        public IEnumerable<QrBox> All => _boxes.Values;
        public int Count => _boxes.Count;

        public bool Register(QrBox box)
        {
            if (box == null || _boxes.ContainsKey(box.Id)) return false;
            _boxes.Add(box.Id, box);
            return true;
        }

        public bool Unregister(BoxId id) => _boxes.Remove(id);

        public bool TryGet(BoxId id, out QrBox box) => _boxes.TryGetValue(id, out box);
    }
}
