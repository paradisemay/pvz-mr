using System;
using System.Collections.Generic;

namespace Pvz.Core
{
    /// <summary>A delivery scheduled for the shift: which boxes arrive and after how many served customers.</summary>
    public sealed class SupplyPlan
    {
        public SupplyPlan(int afterOrders, IEnumerable<BoxRecord> boxes)
        {
            if (afterOrders < 0) throw new ArgumentOutOfRangeException(nameof(afterOrders));
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));
            AfterOrders = afterOrders;
            Boxes = new List<BoxRecord>(boxes).AsReadOnly();
        }

        /// <summary>The delivery is due once this many orders were completed (or earlier if the warehouse runs empty).</summary>
        public int AfterOrders { get; }

        public IReadOnlyList<BoxRecord> Boxes { get; }

        public static SupplyPlan None => new SupplyPlan(0, Array.Empty<BoxRecord>());

        public bool IsEmpty => Boxes.Count == 0;
    }
}
