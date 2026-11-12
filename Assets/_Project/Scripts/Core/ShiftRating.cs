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
        public static ShiftGrade Grade(int ordersCompleted, int ordersTotal, int wrongHandovers)
        {
            if (ordersTotal <= 0) return ShiftGrade.Poor;
            var served = Math.Min(1.0, ordersCompleted / (double)ordersTotal);
            if (served >= 1.0 && wrongHandovers == 0) return ShiftGrade.Excellent;
            if (served >= 1.0 && wrongHandovers <= 1) return ShiftGrade.Good;
            if (served >= 0.5) return ShiftGrade.Satisfactory;
            return ShiftGrade.Poor;
        }
    }
}
