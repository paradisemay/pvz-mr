using System;
using System.Collections.Generic;

namespace Pvz.Core
{
    /// <summary>Where a customer's visit stands.</summary>
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
    /// Every customer of the shift with their state (concept: «интерфейс заказов»). Kept by <see cref="Shift"/>,
    /// which moves entries on as visits join, reach the counter and end; read-only for everyone else.
    /// </summary>
    public sealed class OrderBook
    {
        private readonly List<OrderBookEntry> _entries = new List<OrderBookEntry>();

        /// <summary>An entry was added or changed state (the state is already updated).</summary>
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

        internal void Add(Visit visit)
        {
            if (visit == null) throw new ArgumentNullException(nameof(visit));
            if (Find(visit) != null) return;
            _entries.Add(new OrderBookEntry(visit));
            Changed?.Invoke();
        }

        /// <summary>Moves an open visit on; a finished visit keeps its final state.</summary>
        internal void Set(Visit visit, VisitState state)
        {
            var entry = visit == null ? null : Find(visit);
            if (entry == null || !entry.IsOpen || entry.State == state) return;
            entry.State = state;
            Changed?.Invoke();
        }

        /// <summary>The open pickup visit for <paramref name="order"/> (the same order can be ordered again after an abandon).</summary>
        internal Visit OpenPickup(Order order) =>
            _entries.Find(e => e.IsOpen && e.Visit.Kind == VisitKind.Pickup && e.Visit.Order == order)?.Visit;
    }
}
