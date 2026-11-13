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

    public enum VisitKind
    {
        /// <summary>Picks up an order from the warehouse.</summary>
        Pickup,
        /// <summary>Brings back a box sold earlier.</summary>
        Return,
    }

    /// <summary>Why a customer stands in line. New kinds extend <see cref="VisitKind"/>.</summary>
    public sealed class Visit
    {
        private Visit(VisitKind kind, Order order, ReturnRequest returnRequest)
        {
            Kind = kind;
            Order = order;
            Return = returnRequest;
        }

        public static Visit Pickup(Order order) =>
            new Visit(VisitKind.Pickup, order ?? throw new ArgumentNullException(nameof(order)), null);

        public static Visit ForReturn(ReturnRequest request) =>
            new Visit(VisitKind.Return, null, request ?? throw new ArgumentNullException(nameof(request)));

        public VisitKind Kind { get; }

        /// <summary>The order to pick up (only for <see cref="VisitKind.Pickup"/>).</summary>
        public Order Order { get; }

        /// <summary>The box being returned (only for <see cref="VisitKind.Return"/>).</summary>
        public ReturnRequest Return { get; }

        public bool IsReturn => Kind == VisitKind.Return;

        /// <summary>The warehouse order this visit holds on to, or null when it reserves nothing.</summary>
        public string ReservedOrderNumber => Kind == VisitKind.Pickup ? Order.Number : null;

        public override string ToString() => IsReturn ? Return.ToString() : $"pickup #{Order.Number}";
    }
}
