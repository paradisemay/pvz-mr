using System;
using System.Collections.Generic;
using System.Linq;

namespace Pvz.Core
{
    /// <summary>A customer order: order number and the boxes that must be handed over.</summary>
    public sealed class Order
    {
        private readonly List<BoxId> _boxes;
        private readonly HashSet<BoxId> _delivered = new HashSet<BoxId>();

        public Order(string number, IEnumerable<BoxId> boxes)
        {
            if (string.IsNullOrWhiteSpace(number))
                throw new ArgumentException("Order number must not be empty.", nameof(number));
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));

            _boxes = boxes.Distinct().ToList();
            if (_boxes.Count == 0) throw new ArgumentException("Order must contain at least one box.", nameof(boxes));
            if (_boxes.Any(b => b.IsEmpty)) throw new ArgumentException("Order contains an empty box id.", nameof(boxes));

            Number = number.Trim();
        }

        public string Number { get; }

        public IReadOnlyList<BoxId> Boxes => _boxes;

        public int DeliveredCount => _delivered.Count;

        public int RemainingCount => _boxes.Count - _delivered.Count;

        public bool IsComplete => RemainingCount == 0;

        public bool Requires(BoxId id) => _boxes.Contains(id) && !_delivered.Contains(id);

        public bool IsDelivered(BoxId id) => _delivered.Contains(id);

        /// <summary>Marks a box as delivered. Returns false if the box is not part of this order or already delivered.</summary>
        internal bool MarkDelivered(BoxId id) => Requires(id) && _delivered.Add(id);

        public override string ToString() => $"#{Number} ({DeliveredCount}/{_boxes.Count})";
    }
}
