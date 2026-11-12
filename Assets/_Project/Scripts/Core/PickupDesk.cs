using System;

namespace Pvz.Core
{
    /// <summary>
    /// The pickup counter: holds the order of the customer currently being served and judges
    /// scans and handovers against it. Pure logic — time is passed in by the caller.
    /// </summary>
    public sealed class PickupDesk
    {
        private readonly BoxCatalog _catalog;

        public PickupDesk(BoxCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public event Action<Order> OrderOpened;
        public event Action<ScanOutcome> BoxScanned;
        public event Action<HandoverOutcome> BoxHandedOver;
        public event Action<OrderResult> OrderCompleted;
        public event Action<HandoverOutcome> WrongHandover;

        public BoxCatalog Catalog => _catalog;
        public Order ActiveOrder { get; private set; }
        public bool HasActiveOrder => ActiveOrder != null;
        public double ActiveOrderOpenedAt { get; private set; }

        /// <summary>Wrong handovers made while serving the active order.</summary>
        public int ActiveOrderMistakes { get; private set; }

        public void OpenOrder(Order order, double now)
        {
            if (order == null) throw new ArgumentNullException(nameof(order));
            if (HasActiveOrder)
                throw new InvalidOperationException($"Order {ActiveOrder.Number} is still being served.");
            foreach (var box in order.Boxes)
            {
                if (!_catalog.Contains(box))
                    throw new ArgumentException($"Order {order.Number} needs box {box} which is not in the catalog.", nameof(order));
            }

            ActiveOrder = order;
            ActiveOrderOpenedAt = now;
            ActiveOrderMistakes = 0;
            OrderOpened?.Invoke(order);
        }

        public ScanOutcome Scan(BoxId box)
        {
            ScanOutcome outcome;
            if (!_catalog.TryGet(box, out var record))
                outcome = new ScanOutcome(ScanStatus.UnknownBox, box, null);
            else if (!HasActiveOrder)
                outcome = new ScanOutcome(ScanStatus.NoActiveOrder, box, record.OrderNumber);
            else if (ActiveOrder.Requires(box))
                outcome = new ScanOutcome(ScanStatus.Matches, box, record.OrderNumber);
            else
                outcome = new ScanOutcome(ScanStatus.OtherOrder, box, record.OrderNumber);

            BoxScanned?.Invoke(outcome);
            return outcome;
        }

        public HandoverOutcome Handover(BoxId box, double now)
        {
            HandoverOutcome outcome;
            if (!_catalog.Contains(box))
            {
                outcome = new HandoverOutcome(HandoverStatus.UnknownBox, box, false);
            }
            else if (!HasActiveOrder)
            {
                outcome = new HandoverOutcome(HandoverStatus.NoActiveOrder, box, false);
            }
            else if (!ActiveOrder.Requires(box))
            {
                ActiveOrderMistakes++;
                outcome = new HandoverOutcome(HandoverStatus.WrongBox, box, false);
                BoxHandedOver?.Invoke(outcome);
                WrongHandover?.Invoke(outcome);
                return outcome;
            }
            else
            {
                ActiveOrder.MarkDelivered(box);
                _catalog.Remove(box);
                var completed = ActiveOrder.IsComplete;
                outcome = new HandoverOutcome(HandoverStatus.Accepted, box, completed);
                BoxHandedOver?.Invoke(outcome);
                if (completed)
                {
                    var result = new OrderResult(ActiveOrder, Math.Max(0, now - ActiveOrderOpenedAt), ActiveOrderMistakes);
                    ActiveOrder = null;
                    ActiveOrderMistakes = 0;
                    OrderCompleted?.Invoke(result);
                }
                return outcome;
            }

            BoxHandedOver?.Invoke(outcome);
            return outcome;
        }
    }
}
