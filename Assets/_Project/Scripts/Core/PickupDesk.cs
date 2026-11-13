using System;

namespace Pvz.Core
{
    /// <summary>
    /// The pickup counter: holds the service session of the customer currently being served and judges
    /// scans and handovers against it. Pure logic — time is passed in by the caller.
    /// State is always fully updated before any event is raised, so handlers may call back into the desk
    /// (e.g. open the next order from <see cref="OrderCompleted"/>).
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
        /// <summary>Raised for every handover attempt, before <see cref="WrongHandover"/> / <see cref="OrderCompleted"/>.</summary>
        public event Action<HandoverOutcome> BoxHandedOver;
        public event Action<HandoverOutcome> WrongHandover;
        public event Action<OrderResult> OrderCompleted;
        /// <summary>Raised for every bag offered, before <see cref="OrderCompleted"/>.</summary>
        public event Action<BagStatus> BagHandedOver;

        public BoxCatalog Catalog => _catalog;
        public ServiceSession ActiveSession { get; private set; }
        public bool HasActiveOrder => ActiveSession != null;
        public Order ActiveOrder => ActiveSession?.Order;

        public void OpenOrder(Order order, double now)
        {
            if (order == null) throw new ArgumentNullException(nameof(order));
            if (HasActiveOrder)
                throw new InvalidOperationException($"Order {ActiveOrder.Number} is still being served.");
            foreach (var box in order.Boxes)
            {
                if (!_catalog.TryGet(box, out var record))
                    throw new ArgumentException($"Order {order.Number} needs box {box} which is not in the catalog.", nameof(order));
                if (record.OrderNumber != order.Number)
                    throw new ArgumentException($"Box {box} belongs to order {record.OrderNumber}, not {order.Number}.", nameof(order));
            }

            ActiveSession = new ServiceSession(order, now);
            OrderOpened?.Invoke(order);
        }

        public ScanOutcome Scan(BoxId box)
        {
            ScanOutcome outcome;
            if (!_catalog.TryGet(box, out var record))
                outcome = new ScanOutcome(ScanStatus.UnknownBox, box, null);
            else if (!HasActiveOrder)
                outcome = new ScanOutcome(ScanStatus.NoActiveOrder, box, record.OrderNumber);
            else if (ActiveSession.Requires(box))
                outcome = new ScanOutcome(ScanStatus.Matches, box, record.OrderNumber);
            else
                outcome = new ScanOutcome(ScanStatus.OtherOrder, box, record.OrderNumber);

            BoxScanned?.Invoke(outcome);
            return outcome;
        }

        public HandoverOutcome Handover(BoxId box, double now)
        {
            if (!_catalog.Contains(box))
                return RaiseHandover(new HandoverOutcome(HandoverStatus.UnknownBox, box, false));
            if (!HasActiveOrder)
                return RaiseHandover(new HandoverOutcome(HandoverStatus.NoActiveOrder, box, false));

            var session = ActiveSession;
            if (!session.Requires(box))
            {
                // Offering the same wrong box again (e.g. it slipped out of the zone and back) is not a new mistake.
                var firstTime = session.Reject(box);
                var wrong = new HandoverOutcome(HandoverStatus.WrongBox, box, false, isRepeat: !firstTime);
                BoxHandedOver?.Invoke(wrong);
                if (firstTime) WrongHandover?.Invoke(wrong);
                return wrong;
            }

            session.MarkDelivered(box);
            _catalog.Remove(box);
            var result = CloseIfComplete(session, now);

            var accepted = new HandoverOutcome(HandoverStatus.Accepted, box, result.HasValue);
            BoxHandedOver?.Invoke(accepted);
            if (result.HasValue) OrderCompleted?.Invoke(result.Value);
            return accepted;
        }

        /// <summary>Gives the current customer a carrier bag.</summary>
        public BagStatus HandOverBag(double now)
        {
            if (!HasActiveOrder)
            {
                BagHandedOver?.Invoke(BagStatus.NoActiveOrder);
                return BagStatus.NoActiveOrder;
            }

            var session = ActiveSession;
            if (!session.DeliverBag())
            {
                BagHandedOver?.Invoke(BagStatus.NotNeeded);
                return BagStatus.NotNeeded;
            }

            var result = CloseIfComplete(session, now);
            BagHandedOver?.Invoke(BagStatus.Accepted);
            if (result.HasValue) OrderCompleted?.Invoke(result.Value);
            return BagStatus.Accepted;
        }

        /// <summary>Ends the session if everything was handed over. State only — events are raised by the caller.</summary>
        private OrderResult? CloseIfComplete(ServiceSession session, double now)
        {
            if (!session.IsComplete) return null;
            ActiveSession = null;
            return new OrderResult(session.Order, Math.Max(0, now - session.OpenedAt), session.Mistakes);
        }

        private HandoverOutcome RaiseHandover(HandoverOutcome outcome)
        {
            BoxHandedOver?.Invoke(outcome);
            return outcome;
        }
    }
}
