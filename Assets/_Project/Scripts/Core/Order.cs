using System;
using System.Collections.Generic;
using System.Linq;

namespace Pvz.Core
{
    /// <summary>
    /// Immutable order definition: order number and the boxes that belong to it, plus what the customer is like
    /// (needs a bag, is in a hurry). Delivery progress lives in <see cref="ServiceSession"/>.
    /// </summary>
    public sealed class Order
    {
        private readonly BoxId[] _boxes;

        public Order(string number, IEnumerable<BoxId> boxes, bool needsBag = false, bool isUrgent = false)
        {
            if (string.IsNullOrWhiteSpace(number))
                throw new ArgumentException("Order number must not be empty.", nameof(number));
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));

            _boxes = boxes.Distinct().ToArray();
            if (_boxes.Length == 0) throw new ArgumentException("Order must contain at least one box.", nameof(boxes));
            if (_boxes.Any(b => b.IsEmpty)) throw new ArgumentException("Order contains an empty box id.", nameof(boxes));

            Number = number.Trim();
            NeedsBag = needsBag;
            IsUrgent = isUrgent;
            Boxes = Array.AsReadOnly(_boxes);
        }

        public string Number { get; }

        public IReadOnlyList<BoxId> Boxes { get; }

        /// <summary>The customer will also ask for a carrier bag.</summary>
        public bool NeedsBag { get; }

        /// <summary>The customer is in a hurry: less patience, bonus points when served.</summary>
        public bool IsUrgent { get; }

        public bool Contains(BoxId id) => Array.IndexOf(_boxes, id) >= 0;

        public override string ToString() => $"#{Number} ({_boxes.Length} box(es))";
    }
}
