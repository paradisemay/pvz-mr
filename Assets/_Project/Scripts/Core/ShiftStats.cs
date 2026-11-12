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

        public ShiftStats(PickupDesk desk, ScoringRules rules = null)
        {
            _desk = desk ?? throw new ArgumentNullException(nameof(desk));
            _rules = rules ?? ScoringRules.Default;
            _desk.BoxScanned += OnBoxScanned;
            _desk.WrongHandover += OnWrongHandover;
            _desk.OrderCompleted += OnOrderCompleted;
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

        /// <summary>A supply box was accepted into the warehouse.</summary>
        public void RecordIntake()
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

        private void OnOrderCompleted(OrderResult result)
        {
            OrdersCompleted++;
            _totalServiceSeconds += result.DurationSeconds;
            if (result.Mistakes == 0) FlawlessOrders++;
            LastOrderPoints = _rules.PointsForOrder(result.DurationSeconds, result.Mistakes);
            Score += LastOrderPoints;
            Changed?.Invoke();
        }
    }
}
