namespace Pvz.Core
{
    public enum ScanStatus
    {
        /// <summary>The box belongs to the order the current customer is waiting for.</summary>
        Matches,
        /// <summary>The box is known but belongs to another order.</summary>
        OtherOrder,
        /// <summary>The box is not in the catalog (already handed over or never registered).</summary>
        UnknownBox,
        /// <summary>The box is known but no customer is waiting at the desk.</summary>
        NoActiveOrder,
        /// <summary>A box from a supply delivery was scanned for the first time and is now in the warehouse.</summary>
        Received,
        /// <summary>The box a returning customer brought was scanned: the return is accepted.</summary>
        ReturnAccepted,
        /// <summary>A customer is at the counter but their code (the barcode on their phone) is not scanned yet (T035).</summary>
        CodeNotScanned,
    }

    public readonly struct ScanOutcome
    {
        public ScanOutcome(ScanStatus status, BoxId box, string boxOrderNumber, bool looksLikeTheWantedOrder = false,
            int itemsInOrder = 0, int itemsScanned = 0)
        {
            Status = status;
            Box = box;
            BoxOrderNumber = boxOrderNumber;
            LooksLikeTheWantedOrder = status == ScanStatus.OtherOrder && looksLikeTheWantedOrder;
            ItemsInOrder = itemsInOrder;
            ItemsScanned = itemsScanned;
        }

        /// <summary>T043 (#83): items in the order of the customer at the counter (0 — no order there).</summary>
        public int ItemsInOrder { get; }

        /// <summary>T043 (#83): of them, found by the scanner (or already handed over), this scan included.</summary>
        public int ItemsScanned { get; }

        public ScanStatus Status { get; }
        public BoxId Box { get; }

        /// <summary>Order number printed on the scanned box, null for unknown boxes.</summary>
        public string BoxOrderNumber { get; }

        /// <summary>
        /// <see cref="ScanStatus.OtherOrder"/> whose number is easy to mix up with the customer's
        /// (<see cref="OrderNumbers.LookAlike"/>): a trap worth a warning.
        /// </summary>
        public bool LooksLikeTheWantedOrder { get; }

        public override string ToString() => $"{Status} {Box} #{BoxOrderNumber}" + (LooksLikeTheWantedOrder ? " (look-alike)" : string.Empty);
    }

    /// <summary>Summary of a completed order, raised by <see cref="PickupDesk.OrderCompleted"/>.</summary>
    public readonly struct OrderResult
    {
        public OrderResult(Order order, double durationSeconds, int mistakes)
        {
            Order = order;
            DurationSeconds = durationSeconds;
            Mistakes = mistakes;
        }

        public Order Order { get; }
        public double DurationSeconds { get; }
        /// <summary>Wrong boxes offered to the customer while serving this order.</summary>
        public int Mistakes { get; }
    }

    public enum BagStatus
    {
        /// <summary>The customer needed a bag and took it.</summary>
        Accepted,
        /// <summary>The customer does not need a bag (or already has one); not a mistake.</summary>
        NotNeeded,
        /// <summary>The customer needs a bag, but only once all boxes are handed over.</summary>
        TooEarly,
        /// <summary>No customer is waiting at the desk.</summary>
        NoActiveOrder,
        /// <summary>The customer's code is not scanned yet (T035).</summary>
        CodeNotScanned,
    }

    public enum HandoverStatus
    {
        /// <summary>Box accepted; check <see cref="HandoverOutcome.OrderCompleted"/> for multi-box orders.</summary>
        Accepted,
        /// <summary>The customer did not order this box.</summary>
        WrongBox,
        /// <summary>The box is not in the catalog.</summary>
        UnknownBox,
        /// <summary>No customer is waiting at the desk.</summary>
        NoActiveOrder,
        /// <summary>The customer's code is not scanned yet: the box stays where it is, no mistake (T035).</summary>
        CodeNotScanned,
    }

    public readonly struct HandoverOutcome
    {
        public HandoverOutcome(HandoverStatus status, BoxId box, bool orderCompleted, bool isRepeat = false,
            bool looksLikeTheWantedOrder = false)
        {
            Status = status;
            Box = box;
            OrderCompleted = orderCompleted;
            IsRepeat = isRepeat;
            LooksLikeTheWantedOrder = status == HandoverStatus.WrongBox && looksLikeTheWantedOrder;
        }

        /// <summary>A <see cref="HandoverStatus.WrongBox"/> whose order number is easy to mix up with the customer's.</summary>
        public bool LooksLikeTheWantedOrder { get; }

        public HandoverStatus Status { get; }
        public BoxId Box { get; }
        public bool OrderCompleted { get; }

        /// <summary>The same wrong box was already offered to this customer; it is not penalised again.</summary>
        public bool IsRepeat { get; }

        public override string ToString() => $"{Status} {Box} completed={OrderCompleted}";
    }
}
