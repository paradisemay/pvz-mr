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
            // Subscribed before anyone else: the order book is up to date when the presenter and director react.
            Desk.OrderOpened += OnOrderOpened;
            Desk.ReturnOpened += OnReturnOpened;
            Desk.OrderCompleted += OnOrderCompleted;
            Desk.OrderAbandoned += OnOrderAbandoned;
            Desk.ReturnAccepted += OnReturnAccepted;
        }

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
            // The line is the book's open visits: serving the visit takes the customer out of the line.
            var visit = Book.OpenPickup(result.Order);
            if (visit != null) Book.Serve(visit);
        }

        // An order opened at the desk without joining the line (tests, single-customer use) is not in the book.
        private void OnOrderOpened(Order order)
        {
            var visit = Book.OpenPickup(order);
            if (visit != null) Book.Open(visit);
        }

        private void OnReturnOpened(ReturnRequest request)
        {
            var visit = OpenReturn(request);
            if (visit != null) Book.Open(visit);
        }

        private void OnOrderAbandoned(Order order)
        {
            var visit = Book.OpenPickup(order);
            if (visit != null) Book.Abandon(visit); // the order goes back to the pool for the next customers
        }

        private void OnReturnAccepted(ReturnRequest request)
        {
            var visit = OpenReturn(request);
            if (visit != null) Book.Serve(visit);
        }

        private Visit OpenReturn(ReturnRequest request)
        {
            foreach (var visit in Book.Line) if (visit.Return == request) return visit;
            return null;
        }

        /// <summary>Every customer of the shift and where their visit stands (the order list on the counter).</summary>
        public OrderBook Book { get; } = new OrderBook();

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
        public bool IsDeliveryDue => HasScheduledDelivery && !IsTimeUp && !Desk.HasCustomer &&
                                     (Stats.OrdersCompleted >= _supplyPlan.AfterOrders || !HasOrdersLeft);

        public bool IsPendingIntake(BoxId id) => _pendingIntake.ContainsKey(id);

        /// <summary>
        /// Everything is sold and received — or the shift time ran out and the last customer at the counter is done
        /// (orders left in the warehouse simply stay unserved).
        /// </summary>
        public bool IsOver => IsTimeUp
            ? !Desk.HasCustomer
            : !HasOrdersLeft && !Desk.HasCustomer && !HasPendingIntake && !HasScheduledDelivery && Book.Line.Count == 0;

        /// <summary>The shift clock (time only; the consequences of running out are handled here, in <see cref="Tick"/>).</summary>
        public ShiftClock Clock { get; } = new ShiftClock();

        /// <summary>Length of the shift, seconds; 0 = no time limit (the shift lasts until everything is sold).</summary>
        public double TimeLimitSeconds
        {
            get => Clock.LimitSeconds;
            set => Clock.LimitSeconds = value;
        }

        public double? StartedAt => Clock.StartedAt;

        /// <summary>The shift time ran out: nobody new joins the line and no delivery comes any more.</summary>
        public bool IsTimeUp => Clock.HasRunOut;

        /// <summary>
        /// The shift time ran out (raised once). The argument lists the customers who were still waiting in line and
        /// went home; the one already at the counter is served to the end.
        /// </summary>
        public event Action<IReadOnlyList<Visit>> TimeUp;

        /// <summary>Starts the shift clock (only the first call counts).</summary>
        public void Start(double now) => Clock.Start(now);

        /// <summary>Seconds of the shift left, or null when there is no time limit or the clock has not started.</summary>
        public double? TimeLeft(double now) => Clock.TimeLeft(now);

        /// <summary>The time ran out: the line goes home (the customer at the counter stays), then <see cref="TimeUp"/>.</summary>
        private void CheckTime(double now)
        {
            // A shift that already ended (everything sold) stops the clock: the time can no longer run out.
            if (!IsTimeUp && IsOver) return;
            if (!Clock.TryRunOut(now)) return;
            var sentAway = new List<Visit>();
            foreach (var visit in Book.Line)
            {
                if (!IsAtCounter(visit)) sentAway.Add(visit);
            }
            Book.Leave(sentAway); // out of the line, with one Changed for all of them
            TimeUp?.Invoke(sentAway); // the line and IsTimeUp are already committed
        }

        private bool IsAtCounter(Visit visit) => visit.Kind == VisitKind.Pickup
            ? Desk.HasActiveOrder && Desk.ActiveOrder == visit.Order
            : Desk.ActiveReturn == visit.Return;

        /// <summary>Replaces the randomness (e.g. a fixed seed in tests). Affects orders created afterwards.</summary>
        public void UseRandomSource(IRandomSource random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _generator = new OrderGenerator(_random, BagChancePercent) {
                UrgentChancePercent = UrgentChancePercent,
                ForgotNumberChancePercent = ForgotNumberChancePercent,
            };
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

        private double _urgentPatienceFactor = 0.6;

        /// <summary>Share (0..1] of the usual patience an urgent customer has.</summary>
        public double UrgentPatienceFactor
        {
            get => _urgentPatienceFactor;
            set
            {
                // 0 would mean "waits forever" — the opposite of urgent.
                if (value <= 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(value), value, "Expected a share in (0, 1].");
                _urgentPatienceFactor = value;
            }
        }

        /// <summary>How long the customer with <paramref name="order"/> waits at the counter, seconds; 0 = forever.</summary>
        public double PatienceFor(Order order) =>
            order != null && order.IsUrgent ? PatienceSeconds * UrgentPatienceFactor : PatienceSeconds;

        /// <summary>
        /// Advances customer patience. Call regularly with the current time.
        /// A customer who has already received part of the order waits until it is complete: leaving with some boxes
        /// would take them out of stock and leave an incomplete order behind.
        /// </summary>
        public void Tick(double now)
        {
            CheckTime(now);
            if (!Desk.HasActiveOrder) return;
            var session = Desk.ActiveSession;
            var patience = PatienceFor(session.Order);
            if (patience <= 0 || session.DeliveredCount > 0) return;
            var waited = now - session.OpenedAt;
            if (!session.Hurried && waited >= patience * HurryFraction)
            {
                session.MarkHurried();
                CustomerHurried?.Invoke(session.Order);
            }
            if (waited >= patience) Desk.Abandon();
        }

        /// <summary>
        /// Share of patience the customer at the counter has left (1 = just arrived, 0 = leaving), or null when
        /// patience is not ticking: no customer, infinite patience, or the order is already being handed over.
        /// </summary>
        public double? PatienceRemaining(double now)
        {
            if (!Desk.HasActiveOrder) return null;
            var session = Desk.ActiveSession;
            var patience = PatienceFor(session.Order);
            if (patience <= 0 || session.DeliveredCount > 0) return null;
            var left = 1.0 - (now - session.OpenedAt) / patience;
            return Math.Max(0.0, Math.Min(1.0, left));
        }

        /// <summary>Probability (0..100) that a customer also asks for a bag.</summary>
        public int BagChancePercent
        {
            get => _generator.BagChancePercent;
            set => _generator.BagChancePercent = value;
        }

        /// <summary>Probability (0..100) that a customer is in a hurry (less patience, bonus points).</summary>
        public int UrgentChancePercent
        {
            get => _generator.UrgentChancePercent;
            set => _generator.UrgentChancePercent = value;
        }

        /// <summary>Probability (0..100) that a customer remembers only the end of the order number.</summary>
        public int ForgotNumberChancePercent
        {
            get => _generator.ForgotNumberChancePercent;
            set => _generator.ForgotNumberChancePercent = value;
        }

        /// <summary>How many customers may wait in line (including the one at the counter).</summary>
        public int QueueLength { get; set; } = 1;

        /// <summary>Most customers that stood in line at the same time during the shift.</summary>
        public int MaxQueueLength { get; private set; }

        /// <summary>Chance (0..100) that a new customer in line comes to return a box instead of picking up an order.</summary>
        public int ReturnChancePercent { get; set; }

        /// <summary>Why each customer in line came, oldest first; the first one is served next.</summary>
        public IReadOnlyList<Visit> QueuedVisits => Book.Line;

        /// <summary>Orders of the pickup customers in line, oldest first.</summary>
        public IReadOnlyList<Order> QueuedOrders
        {
            get
            {
                var orders = new List<Order>();
                foreach (var visit in Book.Line) if (visit.Kind == VisitKind.Pickup) orders.Add(visit.Order);
                return orders;
            }
        }

        public bool CanEnqueue => !IsTimeUp && Book.Line.Count < Math.Max(1, QueueLength) && HasUnreservedOrders;

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
            visit = Book.Line[Book.Line.Count - 1];
            return true;
        }

        /// <summary>
        /// Reserves an order for a new pickup customer regardless of <see cref="QueueLength"/> (tests and single-customer use).
        /// Returns null when every order in the warehouse is already taken by someone.
        /// </summary>
        public Order NextOrder()
        {
            var order = _generator.CreateRandomOrder(Desk.Catalog, ReservedNumbers(), KnownOrderNumbers());
            if (order == null) return null;
            Join(Visit.Pickup(order));
            return order;
        }

        private void Join(Visit visit)
        {
            Book.Join(visit);
            MaxQueueLength = Math.Max(MaxQueueLength, Book.Line.Count);
        }

        /// <summary>The first customer in line reached the counter: their order (or return) is opened.</summary>
        public Visit OpenNext(double now)
        {
            if (Book.Line.Count == 0) throw new InvalidOperationException("Nobody is waiting in line.");
            var visit = Book.Line[0];
            switch (visit.Kind)
            {
                case VisitKind.Return: Desk.OpenReturn(visit.Return); break;
                default: Desk.OpenOrder(visit.Order, now); break;
            }
            return visit; // the book marks the visit «at the counter» from the desk's Opened events
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

        public bool IsReserved(string orderNumber)
        {
            foreach (var visit in Book.Line) if (visit.ReservedOrderNumber == orderNumber) return true;
            return false;
        }

        /// <summary>A customer left the line before reaching the counter; their order (or return) is dropped.</summary>
        public void LeaveQueue(Visit visit)
        {
            if (visit == null) return;
            if (IsAtCounter(visit)) return;
            var entry = Book.Find(visit);
            if (entry != null && entry.State == VisitState.Waiting) Book.Leave(new[] { visit });
        }

        /// <summary>Same as <see cref="LeaveQueue(Visit)"/> for a pickup customer.</summary>
        public void LeaveQueue(Order order)
        {
            var visit = Book.OpenPickup(order);
            if (visit != null) LeaveQueue(visit);
        }

        /// <summary>
        /// Every order this shift has or will have: stock, deliveries waiting to be scanned and the planned delivery.
        /// A forgetful customer's hint must stay unambiguous against all of them.
        /// </summary>
        private IEnumerable<string> KnownOrderNumbers()
        {
            foreach (var number in _orderNumbers) yield return number;
            if (!HasScheduledDelivery) yield break;
            foreach (var box in _supplyPlan.Boxes) yield return box.OrderNumber;
        }

        private HashSet<string> ReservedNumbers()
        {
            var numbers = new HashSet<string>();
            foreach (var visit in Book.Line)
            {
                if (visit.ReservedOrderNumber != null) numbers.Add(visit.ReservedOrderNumber);
            }
            return numbers;
        }

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
            Desk.OrderAbandoned -= OnOrderAbandoned;
            Desk.OrderOpened -= OnOrderOpened;
            Desk.ReturnOpened -= OnReturnOpened;
            Desk.ReturnAccepted -= OnReturnAccepted;
            Stats.Dispose();
        }
    }
}