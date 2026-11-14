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
            _desk.ReturnAccepted += OnReturnAccepted;
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

        /// <summary>Returns taken back from customers.</summary>
        public int ReturnsAccepted { get; private set; }

        /// <summary>Urgent orders handed over in full.</summary>
        public int UrgentServed { get; private set; }

        /// <summary>T047 (#86): orders whose box was missing, reported with «Не найден».</summary>
        public int LostOrders { get; private set; }

        /// <summary>T047: «Не найден» pressed while every box of the order was in the warehouse.</summary>
        public int FalseNotFound { get; private set; }

        internal void OnLostOrder()
        {
            LostOrders++;
            Score += _rules.LostOrderReported;
            Changed?.Invoke();
        }

        internal void OnFalseNotFound()
        {
            FalseNotFound++;
            Score = Math.Max(0, Score - _rules.FalseNotFoundPenalty); // never below zero, like the other penalties
            Changed?.Invoke();
        }

        public int PointsPerLostOrder => _rules.LostOrderReported;
        public int FalseNotFoundPenaltyPoints => _rules.FalseNotFoundPenalty;

        /// <summary>T045: points for every return handed in from the bin.</summary>
        public int PointsPerReturnHandedIn => _rules.ReturnHandedIn;

        /// <summary>T045 (#85): boxes customers checked at the counter and refused.</summary>
        public int Refusals { get; private set; }

        /// <summary>T045: returned and refused boxes handed in from the returns bin.</summary>
        public int ReturnsHandedIn { get; private set; }

        /// <summary>T045: returned and refused boxes left outside the bin when the shift ended.</summary>
        public int ReturnsLost { get; private set; }

        internal void OnRefusal()
        {
            Refusals++;
            Changed?.Invoke();
        }

        internal void OnReturnsHandedIn(int count)
        {
            if (count <= 0) return;
            ReturnsHandedIn += count;
            Score += count * _rules.ReturnHandedIn;
            Changed?.Invoke();
        }

        internal void OnReturnsLost(int count)
        {
            if (count <= 0) return;
            ReturnsLost += count;
            Score = Math.Max(0, Score - count * _rules.ReturnLostPenalty);
            Changed?.Invoke();
        }

        /// <summary>T044 (#84): customers who came while the line was full and did not stay.</summary>
        public int CustomersTurnedAway { get; private set; }

        internal void OnTurnedAway()
        {
            CustomersTurnedAway++;
            Changed?.Invoke();
        }

        /// <summary>Points every accepted return gives.</summary>
        public int PointsPerReturn => _rules.ReturnService;

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
            _desk.ReturnAccepted -= OnReturnAccepted;
        }

        private void OnBoxScanned(ScanOutcome outcome)
        {
            if (outcome.Status == ScanStatus.CodeNotScanned) return; // the box was not looked at: the code comes first (T035)
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

        private void OnReturnAccepted(ReturnRequest request)
        {
            ReturnsAccepted++;
            Score += _rules.ReturnService;
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
            if (result.Order.IsUrgent)
            {
                UrgentServed++;
                orderPoints += _rules.UrgentBonus;
            }
            Score += orderPoints;
            // Points shown for the order include the bag, which was already added to the score when handed over.
            LastOrderPoints = orderPoints + _bagPointsThisOrder;
            _bagPointsThisOrder = 0;
            Changed?.Invoke();
        }
    }
}
