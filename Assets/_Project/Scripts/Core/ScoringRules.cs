namespace Pvz.Core
{
    /// <summary>Points awarded and deducted during a shift. Defaults follow docs/context/tracks/T002-order-core/spec.md.</summary>
    public sealed class ScoringRules
    {
        public int OrderCompleted { get; set; } = 100;
        public int FlawlessBonus { get; set; } = 50;
        public double FastThresholdSeconds { get; set; } = 30;
        public int FastBonus { get; set; } = 50;
        public double OkThresholdSeconds { get; set; } = 60;
        public int OkBonus { get; set; } = 25;
        public int WrongHandoverPenalty { get; set; } = 25;
        public int IntakeBox { get; set; } = 10;
        public int BagService { get; set; } = 20;
        public int AbandonPenalty { get; set; } = 50;

        public static ScoringRules Default => new ScoringRules();

        /// <summary>Points for a completed order given how long it took and how many wrong boxes were offered.</summary>
        public int PointsForOrder(double durationSeconds, int mistakes)
        {
            var points = OrderCompleted;
            if (mistakes == 0) points += FlawlessBonus;
            if (durationSeconds <= FastThresholdSeconds) points += FastBonus;
            else if (durationSeconds <= OkThresholdSeconds) points += OkBonus;
            return points;
        }
    }
}
