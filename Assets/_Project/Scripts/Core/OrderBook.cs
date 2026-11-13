using System;
using System.Collections.Generic;

namespace Pvz.Core
{
    /// <summary>Where a customer's visit stands. Transitions: Waiting → AtCounter → Served | Abandoned; Waiting → Left.</summary>
    public enum VisitState
    {
        /// <summary>In line, not at the counter yet.</summary>
        Waiting,
        /// <summary>Being served at the counter.</summary>
        AtCounter,
        /// <summary>The order was handed over in full, or the return was accepted.</summary>
        Served,
        /// <summary>Ran out of patience at the counter; the order stays in the warehouse.</summary>
        Abandoned,
        /// <summary>Left the line before reaching the counter (vanished on the way, or the shift time ran out).</summary>
        Left,
    }

    /// <summary>One visit of the shift and its current state.</summary>
    public sealed class OrderBookEntry
    {
        internal OrderBookEntry(Visit visit) => Visit = visit;

        public Visit Visit { get; }
        public VisitState State { get; internal set; }

        public bool IsOpen => State == VisitState.Waiting || State == VisitState.AtCounter;

        public override string ToString() => $"{Visit}: {State}";
    }

    /// <summary>
    /// Every customer of the shift with their state (concept: «интерфейс заказов»). Kept by <see cref="Shift"/>; the
    /// allowed transitions are enforced here (an illegal one throws). Read-only for everyone else.
    /// </summary>
    public sealed class OrderBook
    {
        private readonly List<OrderBookEntry> _entries = new List<OrderBookEntry>();

        /// <summary>The book changed; raised once per change, after the state (and the shift's line) is updated.</summary>
        public event Action Changed;

        /// <summary>All visits of the shift, oldest first.</summary>
        public IReadOnlyList<OrderBookEntry> Entries => _entries;

        public int Count(VisitState state)
        {
            var count = 0;
            foreach (var entry in _entries) if (entry.State == state) count++;
            return count;
        }

        public OrderBookEntry Find(Visit visit) => _entries.Find(e => e.Visit == visit);

        /// <summary>The visits still open (waiting or at the counter), oldest first.</summary>
        public List<Visit> OpenVisits() => _entries.FindAll(e => e.IsOpen).ConvertAll(e => e.Visit);

        internal void Join(Visit visit)
        {
            if (visit == null) throw new ArgumentNullException(nameof(visit));
            if (Find(visit) != null) throw new InvalidOperationException($"{visit} is already in the book.");
            _entries.Add(new OrderBookEntry(visit));
            Changed?.Invoke();
        }

        /// <summary>The customer reached the counter.</summary>
        internal void Open(Visit visit) => Move(visit, VisitState.Waiting, VisitState.AtCounter);

        /// <summary>The order was handed over in full, or the return accepted.</summary>
        internal void Serve(Visit visit) => Move(visit, VisitState.AtCounter, VisitState.Served);

        /// <summary>The customer at the counter ran out of patience.</summary>
        internal void Abandon(Visit visit) => Move(visit, VisitState.AtCounter, VisitState.Abandoned);

        /// <summary>Customers left the line before reaching the counter (one <see cref="Changed"/> for all of them).</summary>
        internal void Leave(IEnumerable<Visit> visits)
        {
            var changed = false;
            foreach (var visit in visits)
            {
                Entry(visit, VisitState.Waiting).State = VisitState.Left;
                changed = true;
            }
            if (changed) Changed?.Invoke();
        }

        /// <summary>The open pickup visit for <paramref name="order"/> (the same order can be ordered again after an abandon).</summary>
        internal Visit OpenPickup(Order order) =>
            _entries.Find(e => e.IsOpen && e.Visit.Kind == VisitKind.Pickup && e.Visit.Order == order)?.Visit;

        private void Move(Visit visit, VisitState from, VisitState to)
        {
            Entry(visit, from).State = to;
            Changed?.Invoke();
        }

        private OrderBookEntry Entry(Visit visit, VisitState expected)
        {
            var entry = visit == null ? null : Find(visit);
            if (entry == null) throw new InvalidOperationException($"{visit?.ToString() ?? "null"} is not in the book.");
            if (entry.State != expected)
                throw new InvalidOperationException($"{visit} is {entry.State}, expected {expected}.");
            return entry;
        }
    }
}
