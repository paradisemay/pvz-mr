using System;
using System.Collections.Generic;
using System.Linq;

namespace Pvz.Core
{
    /// <summary>
    /// Immutable order definition: order number and the boxes that belong to it, plus what the customer is like
    /// (needs a bag, is in a hurry, forgot the number). Delivery progress lives in <see cref="ServiceSession"/>.
    /// </summary>
    public sealed class Order
    {
        /// <summary>How many trailing digits a forgetful customer still remembers.</summary>
        public const int HintDigits = 2;

        private readonly BoxId[] _boxes;

        public Order(string number, IEnumerable<BoxId> boxes, bool needsBag = false, bool isUrgent = false,
            bool forgotNumber = false)
        {
            if (string.IsNullOrWhiteSpace(number))
                throw new ArgumentException("Order number must not be empty.", nameof(number));
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));

            _boxes = boxes.Distinct().ToArray();
            if (_boxes.Length == 0) throw new ArgumentException("Order must contain at least one box.", nameof(boxes));
            if (_boxes.Any(b => b.IsEmpty)) throw new ArgumentException("Order contains an empty box id.", nameof(boxes));

            Number = number.Trim();
            if (forgotNumber && !CanHint(Number))
                throw new ArgumentException($"Order number {Number} is too short to remember only its end.", nameof(forgotNumber));
            NeedsBag = needsBag;
            IsUrgent = isUrgent;
            ForgotNumber = forgotNumber;
            Boxes = Array.AsReadOnly(_boxes);
        }

        public string Number { get; }

        public IReadOnlyList<BoxId> Boxes { get; }

        /// <summary>The customer will also ask for a carrier bag.</summary>
        public bool NeedsBag { get; }

        /// <summary>The customer is in a hurry: less patience, bonus points when served.</summary>
        public bool IsUrgent { get; }

        /// <summary>The customer remembers only the end of the number (<see cref="NumberHint"/>).</summary>
        public bool ForgotNumber { get; }

        /// <summary>The last <see cref="HintDigits"/> characters of the number: all a forgetful customer can say.</summary>
        public string NumberHint => HintOf(Number);

        public bool Contains(BoxId id) => Array.IndexOf(_boxes, id) >= 0;

        /// <summary>The end of <paramref name="number"/> a forgetful customer remembers.</summary>
        public static string HintOf(string number) => number.Substring(Math.Max(0, number.Length - HintDigits));

        /// <summary>A number can be half-forgotten only if something is left to forget.</summary>
        public static bool CanHint(string number) => number != null && number.Length > HintDigits;

        public override string ToString() => $"#{Number} ({_boxes.Length} box(es))";
    }
}
