using System;

namespace Pvz.Core
{
    /// <summary>
    /// Score and statistics of one shift. Subscribes to a <see cref="PickupDesk"/> and keeps counting
    /// until <see cref="Dispose"/> is called.
    /// </summary>
    public sealed class ShiftStats : IDisposable
    {
        private readonly PickupDesk _desk;
        private readonly ScoringRules _rules;
        private double _totalServiceSeconds;
        private int _bagPointsThisOrder;

        public ShiftStats(PickupDesk desk, ScoringRules rules = null)
        {
            _desk = desk ?? throw new ArgumentNullException(nameof(desk));
            _rules = rules ?? ScoringRules.Default;
            _desk.BoxScanned += OnBoxScanned;
            _desk.WrongHandover += OnWrongHandover;
            _desk.OrderCompleted += OnOrderCompleted;
            _desk.BagHandedOver += OnBagHandedOver;
            _desk.OrderAbandoned += OnOrderAbandoned;
            _desk.BoxHandedOver += OnBoxHandedOver;
        }

        public event Action Changed;

        public int Score { get; private set; }
        public int OrdersCompleted { get; private set; }
        public int Scans { get; private set; }
        public int WrongScans { get; private set; }
        public int WrongHandovers { get; private set; }
        public int FlawlessOrders { get; private set; }
        public double AverageServiceSeconds => OrdersCompleted == 0 ? 0 : _totalServiceSeconds / OrdersCompleted;
        public int LastOrderPoints { get; private set; }
        public int BoxesReceived { get; private set; }
        public int BagsGiven { get; private set; }
        public int OrdersAbandoned { get; private set; }

        /// <summary>Boxes handed over to customers (each box of a multi-box order counts).</summary>
        public int BoxesHandedOver { get; private set; }

        /// <summary>Handler for <see cref="Shift.BoxReceived"/>: a supply box was accepted into the warehouse.</summary>
        internal void OnBoxReceived(BoxRecord box)
        {
            BoxesReceived++;
            Score += _rules.IntakeBox;
            Changed?.Invoke();
        }

        public void Dispose()
        {
            _desk.BoxScanned -= OnBoxScanned;
            _desk.WrongHandover -= OnWrongHandover;
            _desk.OrderCompleted -= OnOrderCompleted;
            _desk.BagHandedOver -= OnBagHandedOver;
            _desk.OrderAbandoned -= OnOrderAbandoned;
            _desk.BoxHandedOver -= OnBoxHandedOver;
        }

        private void OnBoxScanned(ScanOutcome outcome)
        {
            Scans++;
            if (outcome.Status == ScanStatus.OtherOrder) WrongScans++;
            Changed?.Invoke();
        }

        private void OnWrongHandover(HandoverOutcome outcome)
        {
            WrongHandovers++;
            Score = Math.Max(0, Score - _rules.WrongHandoverPenalty);
            Changed?.Invoke();
        }

        private void OnBagHandedOver(BagStatus status)
        {
            if (status != BagStatus.Accepted) return;
            BagsGiven++;
            Score += _rules.BagService;
            _bagPointsThisOrder += _rules.BagService;
            Changed?.Invoke();
        }

        private void OnBoxHandedOver(HandoverOutcome outcome)
        {
            if (outcome.Status != HandoverStatus.Accepted) return;
            BoxesHandedOver++;
            Changed?.Invoke();
        }

        private void OnOrderAbandoned(Order order)
        {
            OrdersAbandoned++;
            Score = Math.Max(0, Score - _rules.AbandonPenalty);
            _bagPointsThisOrder = 0;
            Changed?.Invoke();
        }

        private void OnOrderCompleted(OrderResult result)
        {
            OrdersCompleted++;
            _totalServiceSeconds += result.DurationSeconds;
            if (result.Mistakes == 0) FlawlessOrders++;
            var orderPoints = _rules.PointsForOrder(result.DurationSeconds, result.Mistakes);
            Score += orderPoints;
            // Points shown for the order include the bag, which was already added to the score when handed over.
            LastOrderPoints = orderPoints + _bagPointsThisOrder;
            _bagPointsThisOrder = 0;
            Changed?.Invoke();
        }
    }
}
