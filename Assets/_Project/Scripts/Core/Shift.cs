using System;
using System.Collections.Generic;

namespace Pvz.Core
{
    /// <summary>
    /// One work shift: the warehouse, the counter, order generation, the scheduled supply delivery and statistics.
    /// The shift is over when the delivery has arrived and every box has been received and handed over.
    /// </summary>
    public sealed class Shift : IDisposable
    {
        private readonly Dictionary<BoxId, BoxRecord> _pendingIntake = new Dictionary<BoxId, BoxRecord>();
        private readonly HashSet<string> _orderNumbers = new HashSet<string>();
        private readonly SupplyPlan _supplyPlan;
        private OrderGenerator _generator;

        public Shift(BoxCatalog catalog, IRandomSource random, ScoringRules rules = null, SupplyPlan supplyPlan = null,
            int bagChancePercent = 0)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            Desk = new PickupDesk(catalog);
            Stats = new ShiftStats(Desk, rules);
            BoxReceived += Stats.OnBoxReceived;
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _generator = new OrderGenerator(_random, bagChancePercent);
            _supplyPlan = supplyPlan ?? SupplyPlan.None;
            foreach (var box in catalog.Boxes) _orderNumbers.Add(box.OrderNumber);
            HasScheduledDelivery = !_supplyPlan.IsEmpty;
            // A served order is gone; an abandoned one goes back to the pool for the next customers.
            Desk.OrderCompleted += OnOrderCompleted;
            Desk.OrderAbandoned += OnOrderClosed;
            Desk.ReturnAccepted += OnReturnAccepted;
        }

        private readonly List<Visit> _line = new List<Visit>();
        private readonly List<BoxRecord> _returnable = new List<BoxRecord>();
        private readonly HashSet<BoxId> _soldIds = new HashSet<BoxId>();
        private IRandomSource _random;

        private void OnOrderCompleted(OrderResult result)
        {
            // Handed-over boxes may come back later in the shift as returns.
            foreach (var box in result.Order.Boxes)
            {
                _returnable.Add(new BoxRecord(box, result.Order.Number));
                _soldIds.Add(box);
            }
            OnOrderClosed(result.Order);
        }

        private void OnReturnAccepted(ReturnRequest request) => _line.RemoveAll(v => v.Return == request);

        /// <summary>A supply box was scanned and moved into the warehouse.</summary>
        public event Action<BoxRecord> BoxReceived;

        /// <summary>A supply delivery has arrived (its boxes are not in the warehouse until scanned).</summary>
        public event Action<IReadOnlyList<BoxRecord>> DeliveryExpected;

        public PickupDesk Desk { get; }
        public ShiftStats Stats { get; }

        /// <summary>Number of different orders of the shift so far (initial stock + arrived deliveries).</summary>
        public int OrdersTotal => _orderNumbers.Count;

        public bool HasOrdersLeft => Desk.Catalog.Count > 0;

        public bool HasPendingIntake => _pendingIntake.Count > 0;

        public int PendingIntakeCount => _pendingIntake.Count;

        /// <summary>Size of the last delivery and how many of its boxes were already received.</summary>
        public int DeliverySize { get; private set; }
        public int DeliveryReceived => DeliverySize - _pendingIntake.Count;

        /// <summary>The planned delivery has not arrived yet.</summary>
        public bool HasScheduledDelivery { get; private set; }

        /// <summary>
        /// The planned delivery should arrive now: enough customers were served, or there is nothing left to sell.
        /// Never due while a customer is being served.
        /// </summary>
        public bool IsDeliveryDue => HasScheduledDelivery && !Desk.HasCustomer &&
                                     (Stats.OrdersCompleted >= _supplyPlan.AfterOrders || !HasOrdersLeft);

        public bool IsPendingIntake(BoxId id) => _pendingIntake.ContainsKey(id);

        public bool IsOver => !HasOrdersLeft && !Desk.HasCustomer && !HasPendingIntake && !HasScheduledDelivery && _line.Count == 0;

        /// <summary>Replaces the randomness (e.g. a fixed seed in tests). Affects orders created afterwards.</summary>
        public void UseRandomSource(IRandomSource random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _generator = new OrderGenerator(_random, BagChancePercent);
        }

        /// <summary>Share of the patience after which the customer starts hurrying the player.</summary>
        public const double HurryFraction = 0.6;

        /// <summary>The customer told the player to hurry up (once per customer).</summary>
        public event Action<Order> CustomerHurried;

        /// <summary>The customer ran out of patience and left; the order's boxes stay in the warehouse.</summary>
        public event Action<Order> OrderAbandoned
        {
            add => Desk.OrderAbandoned += value;
            remove => Desk.OrderAbandoned -= value;
        }

        /// <summary>How long a customer waits at the counter, seconds; 0 = forever.</summary>
        public double PatienceSeconds { get; set; }

        /// <summary>
        /// Advances customer patience. Call regularly with the current time.
        /// A customer who has already received part of the order waits until it is complete: leaving with some boxes
        /// would take them out of stock and leave an incomplete order behind.
        /// </summary>
        public void Tick(double now)
        {
            if (PatienceSeconds <= 0 || !Desk.HasActiveOrder) return;
            var session = Desk.ActiveSession;
            if (session.DeliveredCount > 0) return;
            var waited = now - session.OpenedAt;
            if (!session.Hurried && waited >= PatienceSeconds * HurryFraction)
            {
                session.MarkHurried();
                CustomerHurried?.Invoke(session.Order);
            }
            if (waited >= PatienceSeconds) Desk.Abandon();
        }

        /// <summary>
        /// Share of patience the customer at the counter has left (1 = just arrived, 0 = leaving), or null when
        /// patience is not ticking: no customer, infinite patience, or the order is already being handed over.
        /// </summary>
        public double? PatienceRemaining(double now)
        {
            if (PatienceSeconds <= 0 || !Desk.HasActiveOrder) return null;
            var session = Desk.ActiveSession;
            if (session.DeliveredCount > 0) return null;
            var left = 1.0 - (now - session.OpenedAt) / PatienceSeconds;
            return Math.Max(0.0, Math.Min(1.0, left));
        }

        /// <summary>Probability (0..100) that a customer also asks for a bag.</summary>
        public int BagChancePercent
        {
            get => _generator.BagChancePercent;
            set => _generator.BagChancePercent = value;
        }

        /// <summary>How many customers may wait in line (including the one at the counter).</summary>
        public int QueueLength { get; set; } = 1;

        /// <summary>Most customers that stood in line at the same time during the shift.</summary>
        public int MaxQueueLength { get; private set; }

        /// <summary>Chance (0..100) that a new customer in line comes to return a box instead of picking up an order.</summary>
        public int ReturnChancePercent { get; set; }

        /// <summary>Why each customer in line came, oldest first; the first one is served next.</summary>
        public IReadOnlyList<Visit> QueuedVisits => _line;

        /// <summary>Orders of the pickup customers in line, oldest first.</summary>
        public IReadOnlyList<Order> QueuedOrders => _line.FindAll(v => v.Kind == VisitKind.Pickup).ConvertAll(v => v.Order);

        public bool CanEnqueue => _line.Count < Math.Max(1, QueueLength) && HasUnreservedOrders;

        /// <summary>
        /// A new customer joins the line. Usually they come for an order nobody in line has yet (it is reserved,
        /// so two customers never share an order); with <see cref="ReturnChancePercent"/> they bring back a box from
        /// an order handed over earlier. Returns only happen while there are still orders to sell, so they never
        /// stretch the end of the shift. False when the line is full or nothing is left.
        /// </summary>
        public bool TryEnqueue(out Visit visit)
        {
            visit = null;
            if (!CanEnqueue) return false;
            if (ReturnChancePercent > 0 && _returnable.Count > 0 && _random.Range(0, 100) < ReturnChancePercent)
            {
                var index = _random.Range(0, _returnable.Count);
                var box = _returnable[index];
                _returnable.RemoveAt(index);
                visit = Visit.ForReturn(new ReturnRequest(box.Id, box.OrderNumber));
                Join(visit);
                return true;
            }
            var order = NextOrder();
            if (order == null) return false;
            visit = _line[_line.Count - 1];
            return true;
        }

        /// <summary>
        /// Reserves an order for a new pickup customer regardless of <see cref="QueueLength"/> (tests and single-customer use).
        /// Returns null when every order in the warehouse is already taken by someone.
        /// </summary>
        public Order NextOrder()
        {
            var order = _generator.CreateRandomOrder(Desk.Catalog, ReservedNumbers());
            if (order == null) return null;
            Join(Visit.Pickup(order));
            return order;
        }

        private void Join(Visit visit)
        {
            _line.Add(visit);
            MaxQueueLength = Math.Max(MaxQueueLength, _line.Count);
        }

        /// <summary>The first customer in line reached the counter: their order (or return) is opened.</summary>
        public Visit OpenNext(double now)
        {
            if (_line.Count == 0) throw new InvalidOperationException("Nobody is waiting in line.");
            var visit = _line[0];
            switch (visit.Kind)
            {
                case VisitKind.Return: Desk.OpenReturn(visit.Return); break;
                default: Desk.OpenOrder(visit.Order, now); break;
            }
            return visit;
        }

        /// <summary>Some order in the warehouse is not promised to any customer yet.</summary>
        public bool HasUnreservedOrders
        {
            get
            {
                var reserved = ReservedNumbers();
                foreach (var box in Desk.Catalog.Boxes)
                {
                    if (!reserved.Contains(box.OrderNumber)) return true;
                }
                return false;
            }
        }

        public bool IsReserved(string orderNumber) => _line.Exists(v => v.ReservedOrderNumber == orderNumber);

        /// <summary>A customer left the line before reaching the counter; their order (or return) is dropped.</summary>
        public void LeaveQueue(Visit visit)
        {
            if (visit == null) return;
            var atCounter = visit.Kind == VisitKind.Pickup
                ? Desk.HasActiveOrder && Desk.ActiveOrder == visit.Order
                : Desk.ActiveReturn == visit.Return;
            if (atCounter) return;
            _line.Remove(visit);
        }

        /// <summary>Same as <see cref="LeaveQueue(Visit)"/> for a pickup customer.</summary>
        public void LeaveQueue(Order order)
        {
            var visit = _line.Find(v => v.Kind == VisitKind.Pickup && v.Order == order);
            if (visit != null) LeaveQueue(visit);
        }

        private HashSet<string> ReservedNumbers()
        {
            var numbers = new HashSet<string>();
            foreach (var visit in _line)
            {
                if (visit.ReservedOrderNumber != null) numbers.Add(visit.ReservedOrderNumber);
            }
            return numbers;
        }

        private void OnOrderClosed(Order order) => _line.RemoveAll(v => v.Kind == VisitKind.Pickup && v.Order == order);

        /// <summary>If the planned delivery is due, makes it arrive and returns its boxes (to be spawned in the room).</summary>
        public bool TryTakeDueDelivery(out IReadOnlyList<BoxRecord> boxes)
        {
            if (!IsDeliveryDue)
            {
                boxes = Array.Empty<BoxRecord>();
                return false;
            }
            HasScheduledDelivery = false;
            boxes = _supplyPlan.Boxes;
            ExpectDelivery(boxes);
            return true;
        }

        /// <summary>Announces an (unplanned) supply delivery. The boxes must be scanned to enter the warehouse.</summary>
        public void ExpectDelivery(IEnumerable<BoxRecord> boxes)
        {
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));
            var delivery = new List<BoxRecord>(boxes);
            // Validate the whole batch before changing anything, so a bad delivery never half-applies.
            var ids = new HashSet<BoxId>();
            foreach (var box in delivery)
            {
                if (box == null) throw new ArgumentException("Delivery contains a null box.", nameof(boxes));
                if (!ids.Add(box.Id) || Desk.Catalog.Contains(box.Id) || _pendingIntake.ContainsKey(box.Id) || _soldIds.Contains(box.Id))
                    throw new ArgumentException($"Box {box.Id} is duplicated, already in the warehouse or on its way.", nameof(boxes));
                // A delivery brings new orders only; topping up an existing order would let it be issued incomplete.
                if (_orderNumbers.Contains(box.OrderNumber))
                    throw new ArgumentException($"Order {box.OrderNumber} already exists in this shift.", nameof(boxes));
            }
            foreach (var box in delivery)
            {
                _pendingIntake.Add(box.Id, box);
                _orderNumbers.Add(box.OrderNumber);
            }
            DeliverySize = _pendingIntake.Count;
            DeliveryExpected?.Invoke(delivery);
        }

        /// <summary>
        /// Scanner input: accepts a delivered box into the warehouse on its first scan,
        /// otherwise asks the counter how the box relates to the current customer.
        /// </summary>
        public ScanOutcome Scan(BoxId id)
        {
            // The box a returning customer brought wins over everything else.
            if (Desk.HasActiveReturn && Desk.ActiveReturn.Box == id) return Desk.Scan(id);
            if (!_pendingIntake.TryGetValue(id, out var record)) return Desk.Scan(id);

            _pendingIntake.Remove(id);
            Desk.Catalog.Add(record);
            BoxReceived?.Invoke(record);
            return new ScanOutcome(ScanStatus.Received, id, record.OrderNumber);
        }

        /// <summary>
        /// Hands over boxes that were already waiting in the handover zone when the customer arrived.
        /// Only boxes the customer actually needs are offered, so waiting boxes never count as mistakes.
        /// </summary>
        public IReadOnlyList<HandoverOutcome> HandOverWaiting(IEnumerable<BoxId> boxesInZone, double now)
        {
            if (boxesInZone == null) throw new ArgumentNullException(nameof(boxesInZone));
            var outcomes = new List<HandoverOutcome>();
            foreach (var box in boxesInZone)
            {
                if (!Desk.HasActiveOrder) break;
                if (Desk.ActiveSession.Requires(box)) outcomes.Add(Desk.Handover(box, now));
            }
            return outcomes;
        }

        public void Dispose()
        {
            BoxReceived -= Stats.OnBoxReceived;
            Desk.OrderCompleted -= OnOrderCompleted;
            Desk.OrderAbandoned -= OnOrderClosed;
            Desk.ReturnAccepted -= OnReturnAccepted;
            Stats.Dispose();
        }
    }
}