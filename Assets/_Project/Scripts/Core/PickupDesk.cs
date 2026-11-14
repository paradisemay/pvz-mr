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
        /// <summary>All boxes are handed over and the customer now asks for a bag.</summary>
        public event Action<Order> BagRequested;
        /// <summary>The customer ran out of patience and left without the order.</summary>
        public event Action<Order> OrderAbandoned;

        /// <summary>The current customer leaves without the order; its boxes stay in the warehouse.</summary>
        public Order Abandon()
        {
            if (!HasActiveOrder) return null;
            var order = ActiveSession.Order;
            ActiveSession = null;
            OrderAbandoned?.Invoke(order);
            return order;
        }

        /// <summary>A returning customer is at the counter.</summary>
        public event Action<ReturnRequest> ReturnOpened;
        /// <summary>The returned box was scanned and taken back.</summary>
        public event Action<ReturnRequest> ReturnAccepted;

        public BoxCatalog Catalog => _catalog;
        public ServiceSession ActiveSession { get; private set; }
        public bool HasActiveOrder => ActiveSession != null;
        public Order ActiveOrder => ActiveSession?.Order;

        /// <summary>The return being processed at the counter, or null.</summary>
        public ReturnRequest ActiveReturn { get; private set; }
        public bool HasActiveReturn => ActiveReturn != null;

        /// <summary>Someone is at the counter (picking up an order or returning a box).</summary>
        public bool HasCustomer => HasActiveOrder || HasActiveReturn;

        /// <summary>
        /// T035: the customer shows a barcode on their phone and nothing is served until it is scanned (scans of their
        /// boxes, handovers and bags wait for it). Off by default: plain Core rules and tests do not need it.
        /// </summary>
        public bool RequiresCustomerCode { get; set; }

        /// <summary>The code of the customer at the counter was scanned (always true when codes are not required).</summary>
        public bool IsCodeScanned { get; private set; }

        /// <summary>Waits for the code of the customer at the counter.</summary>
        public bool AwaitsCode => HasCustomer && !IsCodeScanned;

        /// <summary>The customer's code was scanned: now they can be served.</summary>
        public event Action CodeScanned;

        /// <summary>Scans the barcode the customer at the counter shows. Returns whether it opened their service.</summary>
        public bool ScanCustomerCode()
        {
            if (!AwaitsCode) return false;
            IsCodeScanned = true;
            CodeScanned?.Invoke();
            return true;
        }

        public void OpenOrder(Order order, double now)
        {
            if (order == null) throw new ArgumentNullException(nameof(order));
            if (HasCustomer)
                throw new InvalidOperationException("Another customer is still being served.");
            foreach (var box in order.Boxes)
            {
                if (!_catalog.TryGet(box, out var record))
                    throw new ArgumentException($"Order {order.Number} needs box {box} which is not in the catalog.", nameof(order));
                if (record.OrderNumber != order.Number)
                    throw new ArgumentException($"Box {box} belongs to order {record.OrderNumber}, not {order.Number}.", nameof(order));
            }

            ActiveSession = new ServiceSession(order, now); // patience counts from here, before the code is scanned
            IsCodeScanned = !RequiresCustomerCode;
            OrderOpened?.Invoke(order);
        }

        /// <summary>A returning customer reached the counter with the box.</summary>
        public void OpenReturn(ReturnRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (HasCustomer) throw new InvalidOperationException("Another customer is still being served.");
            if (_catalog.Contains(request.Box))
                throw new ArgumentException($"Box {request.Box} is still in the warehouse; it cannot be returned.", nameof(request));
            ActiveReturn = request;
            IsCodeScanned = !RequiresCustomerCode;
            ReturnOpened?.Invoke(request);
        }

        public ScanOutcome Scan(BoxId box)
        {
            // Before the customer's code nothing about them is known: their box, the return or another order alike.
            if (AwaitsCode && (_catalog.TryGet(box, out _) || (HasActiveReturn && ActiveReturn.Box == box)))
            {
                var waiting = new ScanOutcome(ScanStatus.CodeNotScanned, box, null);
                BoxScanned?.Invoke(waiting);
                return waiting;
            }
            if (HasActiveReturn && ActiveReturn.Box == box)
            {
                var request = ActiveReturn;
                ActiveReturn = null;
                var accepted = new ScanOutcome(ScanStatus.ReturnAccepted, box, request.OrderNumber);
                BoxScanned?.Invoke(accepted);
                ReturnAccepted?.Invoke(request);
                return accepted;
            }

            ScanOutcome outcome;
            if (!_catalog.TryGet(box, out var record))
                outcome = new ScanOutcome(ScanStatus.UnknownBox, box, null);
            else if (!HasActiveOrder)
                outcome = new ScanOutcome(ScanStatus.NoActiveOrder, box, record.OrderNumber);
            else if (ActiveSession.Requires(box))
            {
                ActiveSession.MarkScanned(box);
                outcome = new ScanOutcome(ScanStatus.Matches, box, record.OrderNumber, false,
                    ActiveSession.Order.Boxes.Count, ActiveSession.ScannedCount);
            }
            else
                outcome = new ScanOutcome(ScanStatus.OtherOrder, box, record.OrderNumber, IsLookAlikeTrap(record.OrderNumber),
                    ActiveSession.Order.Boxes.Count, ActiveSession.ScannedCount);

            BoxScanned?.Invoke(outcome);
            return outcome;
        }

        public HandoverOutcome Handover(BoxId box, double now)
        {
            if (!_catalog.Contains(box))
                return RaiseHandover(new HandoverOutcome(HandoverStatus.UnknownBox, box, false));
            if (!HasActiveOrder)
                return RaiseHandover(new HandoverOutcome(HandoverStatus.NoActiveOrder, box, false));
            if (AwaitsCode)
                return RaiseHandover(new HandoverOutcome(HandoverStatus.CodeNotScanned, box, false));

            var session = ActiveSession;
            if (!session.Requires(box))
            {
                // Offering the same wrong box again (e.g. it slipped out of the zone and back) is not a new mistake.
                var firstTime = session.Reject(box);
                _catalog.TryGet(box, out var wrongRecord);
                var wrong = new HandoverOutcome(HandoverStatus.WrongBox, box, false, isRepeat: !firstTime,
                    looksLikeTheWantedOrder: IsLookAlikeTrap(wrongRecord?.OrderNumber));
                BoxHandedOver?.Invoke(wrong);
                if (firstTime) WrongHandover?.Invoke(wrong);
                return wrong;
            }

            session.MarkDelivered(box);
            _catalog.Remove(box);
            var result = CloseIfComplete(session, now);

            var accepted = new HandoverOutcome(HandoverStatus.Accepted, box, result.HasValue);
            var asksForBag = session.AwaitingBag;
            BoxHandedOver?.Invoke(accepted);
            if (result.HasValue) OrderCompleted?.Invoke(result.Value);
            else if (asksForBag) BagRequested?.Invoke(session.Order);
            return accepted;
        }

        /// <summary>A box of another order whose number is easy to mix up with the customer's.</summary>
        private bool IsLookAlikeTrap(string boxOrderNumber) =>
            HasActiveOrder && OrderNumbers.LookAlike(boxOrderNumber, ActiveOrder.Number);

        /// <summary>Gives the current customer a carrier bag.</summary>
        public BagStatus HandOverBag(double now)
        {
            if (!HasActiveOrder)
            {
                BagHandedOver?.Invoke(BagStatus.NoActiveOrder);
                return BagStatus.NoActiveOrder;
            }
            if (AwaitsCode)
            {
                BagHandedOver?.Invoke(BagStatus.CodeNotScanned);
                return BagStatus.CodeNotScanned;
            }

            var session = ActiveSession;
            var status = session.DeliverBag();
            if (status != BagStatus.Accepted)
            {
                BagHandedOver?.Invoke(status);
                return status;
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
