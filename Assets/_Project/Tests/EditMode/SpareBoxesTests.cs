using System.Linq;
using NUnit.Framework;

namespace Pvz.Core.Tests
{
    /// <summary>T036 (#80): spare boxes in the pile — other days' orders nobody picks up this shift.</summary>
    public class SpareBoxesTests
    {
        private static Shift ShiftWithSpare()
        {
            var catalog = TestData.MvpCatalog();
            catalog.Add(new BoxRecord(new BoxId("BOX-201"), "8804"));
            var shift = new Shift(catalog, new SystemRandomSource(11)) { QueueLength = 4 };
            shift.MarkNotForSale(new[] { "8804" });
            return shift;
        }

        [Test]
        public void NobodyComesForASpareOrder_AndItDoesNotCountTowardsTheShift()
        {
            using (var shift = ShiftWithSpare())
            {
                Assert.AreEqual(3, shift.OrdersTotal, "only the shift's own orders");
                Assert.IsFalse(shift.IsForSale("8804"));
                while (shift.TryEnqueue(out var visit)) Assert.AreNotEqual("8804", visit.Order.Number);
                Assert.AreEqual(3, shift.QueuedVisits.Count);
            }
        }

        [Test]
        public void TheShiftEnds_WithTheSpareStillInTheWarehouse()
        {
            using (var shift = ShiftWithSpare())
            {
                var now = 0.0;
                while (shift.TryEnqueue(out _)) { }
                while (shift.QueuedVisits.Count > 0)
                {
                    var visit = shift.OpenNext(now);
                    foreach (var box in visit.Order.Boxes) shift.Desk.Handover(box, now += 1);
                }
                Assert.IsFalse(shift.HasOrdersLeft);
                Assert.IsTrue(shift.IsOver);
                Assert.IsTrue(shift.Desk.Catalog.Contains(new BoxId("BOX-201")), "the spare stays for another day");
            }
        }

        [Test]
        public void ASpareBox_IsScannedAsAnotherOrder()
        {
            using (var shift = ShiftWithSpare())
            {
                Assert.IsTrue(shift.TryEnqueue(out var visit));
                shift.OpenNext(0);
                var outcome = shift.Scan(new BoxId("BOX-201"));
                Assert.AreEqual(ScanStatus.OtherOrder, outcome.Status);
                Assert.AreEqual("8804", outcome.BoxOrderNumber);
            }
        }
    }
}
