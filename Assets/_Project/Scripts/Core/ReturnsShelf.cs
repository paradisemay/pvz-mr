using System;
using System.Collections.Generic;

namespace Pvz.Core
{
    public enum ReturnKind
    {
        /// <summary>A customer came back with a box of an order handed over earlier (T013).</summary>
        Return,
        /// <summary>A customer checked a box at the counter and refused it (T045).</summary>
        Refusal,
    }

    /// <summary>
    /// T045 (#85): the boxes that came back — returns and refusals. The player puts them into the returns bin; the
    /// computer's «Сдать возвраты» hands in what is in the bin (points); at the end of the shift whatever is still in
    /// the bin goes too, and what lies elsewhere is lost (a penalty).
    /// </summary>
    public sealed class ReturnsShelf
    {
        private readonly Dictionary<BoxId, (string order, ReturnKind kind, bool inBin)> _boxes =
            new Dictionary<BoxId, (string, ReturnKind, bool)>();

        public event Action Changed;

        /// <summary>Boxes that came back and are not handed in yet.</summary>
        public int Pending => _boxes.Count;

        public int InBin
        {
            get
            {
                var count = 0;
                foreach (var entry in _boxes.Values) if (entry.inBin) count++;
                return count;
            }
        }

        public bool Expects(BoxId id) => _boxes.ContainsKey(id);

        public bool IsInBin(BoxId id) => _boxes.TryGetValue(id, out var entry) && entry.inBin;

        public ReturnKind? KindOf(BoxId id) => _boxes.TryGetValue(id, out var entry) ? entry.kind : (ReturnKind?)null;

        internal void Expect(BoxId id, string orderNumber, ReturnKind kind)
        {
            _boxes[id] = (orderNumber, kind, false);
            Changed?.Invoke();
        }

        /// <summary>A box landed in the returns bin; false when it is not a return (other boxes do not belong there).</summary>
        public bool Stow(BoxId id) => SetInBin(id, true);

        /// <summary>A box was taken out of the returns bin again.</summary>
        public bool Unstow(BoxId id) => SetInBin(id, false);

        private bool SetInBin(BoxId id, bool inBin)
        {
            if (!_boxes.TryGetValue(id, out var entry)) return false;
            if (entry.inBin == inBin) return true;
            _boxes[id] = (entry.order, entry.kind, inBin);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Takes the boxes in the bin away (handed in); returns them.</summary>
        internal List<BoxId> TakeBin()
        {
            var taken = new List<BoxId>();
            foreach (var pair in _boxes) if (pair.Value.inBin) taken.Add(pair.Key);
            foreach (var id in taken) _boxes.Remove(id);
            if (taken.Count > 0) Changed?.Invoke();
            return taken;
        }

        /// <summary>The end of the shift: the boxes not in the bin are lost; returns them.</summary>
        internal List<BoxId> TakeLost()
        {
            var lost = new List<BoxId>(_boxes.Keys);
            _boxes.Clear();
            if (lost.Count > 0) Changed?.Invoke();
            return lost;
        }
    }
}
