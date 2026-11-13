using System;

namespace Pvz.Core
{
    public enum ShiftGrade
    {
        Poor,
        Satisfactory,
        Good,
        Excellent,
    }

    /// <summary>Final grade of a shift from served orders and mistakes.</summary>
    public static class ShiftRating
    {
        /// <param name="abandoned">Customers who left without their order count as mistakes.</param>
        public static ShiftGrade Grade(int ordersCompleted, int ordersTotal, int wrongHandovers, int abandoned = 0)
        {
            if (ordersTotal <= 0) return ShiftGrade.Poor;
            var mistakes = wrongHandovers + abandoned;
            var served = Math.Min(1.0, ordersCompleted / (double)ordersTotal);
            if (served >= 1.0 && mistakes == 0) return ShiftGrade.Excellent;
            if (served >= 1.0 && mistakes <= 1) return ShiftGrade.Good;
            if (served >= 0.5) return ShiftGrade.Satisfactory;
            return ShiftGrade.Poor;
        }
    }
}
