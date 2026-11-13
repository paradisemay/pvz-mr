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
    }

    public readonly struct ScanOutcome
    {
        public ScanOutcome(ScanStatus status, BoxId box, string boxOrderNumber)
        {
            Status = status;
            Box = box;
            BoxOrderNumber = boxOrderNumber;
        }

        public ScanStatus Status { get; }
        public BoxId Box { get; }

        /// <summary>Order number printed on the scanned box, null for unknown boxes.</summary>
        public string BoxOrderNumber { get; }

        public override string ToString() => $"{Status} {Box} #{BoxOrderNumber}";
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
        /// <summary>No customer is waiting at the desk.</summary>
        NoActiveOrder,
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
    }

    public readonly struct HandoverOutcome
    {
        public HandoverOutcome(HandoverStatus status, BoxId box, bool orderCompleted, bool isRepeat = false)
        {
            Status = status;
            Box = box;
            OrderCompleted = orderCompleted;
            IsRepeat = isRepeat;
        }

        public HandoverStatus Status { get; }
        public BoxId Box { get; }
        public bool OrderCompleted { get; }

        /// <summary>The same wrong box was already offered to this customer; it is not penalised again.</summary>
        public bool IsRepeat { get; }

        public override string ToString() => $"{Status} {Box} completed={OrderCompleted}";
    }
}
