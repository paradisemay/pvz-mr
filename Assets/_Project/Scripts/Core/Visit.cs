using System;

namespace Pvz.Core
{
    /// <summary>A customer brings back a box from an order handed over earlier in the shift.</summary>
    public sealed class ReturnRequest
    {
        public ReturnRequest(BoxId box, string orderNumber)
        {
            if (box.IsEmpty) throw new ArgumentException("Return must name a box.", nameof(box));
            if (string.IsNullOrWhiteSpace(orderNumber)) throw new ArgumentException("Return must name an order.", nameof(orderNumber));
            Box = box;
            OrderNumber = orderNumber.Trim();
        }

        public BoxId Box { get; }
        public string OrderNumber { get; }

        public override string ToString() => $"return {Box} (#{OrderNumber})";
    }

    /// <summary>Why a customer stands in line: to pick up an order or to return a box.</summary>
    public sealed class Visit
    {
        private Visit(Order order, ReturnRequest returnRequest)
        {
            Order = order;
            Return = returnRequest;
        }

        public static Visit Pickup(Order order) => new Visit(order ?? throw new ArgumentNullException(nameof(order)), null);
        public static Visit ForReturn(ReturnRequest request) => new Visit(null, request ?? throw new ArgumentNullException(nameof(request)));

        /// <summary>The order to pick up (null for a return).</summary>
        public Order Order { get; }

        /// <summary>The box being returned (null for a pickup).</summary>
        public ReturnRequest Return { get; }

        public bool IsReturn => Return != null;

        public override string ToString() => IsReturn ? Return.ToString() : $"pickup #{Order.Number}";
    }
}
