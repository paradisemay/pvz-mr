using System;

namespace Pvz.Core
{
    /// <summary>A box stored in the pickup point: which QR it carries and which order it belongs to.</summary>
    public sealed class BoxRecord
    {
        public BoxRecord(BoxId id, string orderNumber)
        {
            if (id.IsEmpty) throw new ArgumentException("Box id must not be empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(orderNumber))
                throw new ArgumentException("Order number must not be empty.", nameof(orderNumber));
            Id = id;
            OrderNumber = orderNumber.Trim();
        }

        public BoxId Id { get; }
        public string OrderNumber { get; }

        public override string ToString() => $"{Id} -> #{OrderNumber}";
    }
}
