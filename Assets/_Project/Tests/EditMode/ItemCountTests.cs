using NUnit.Framework;

namespace Pvz.Core.Tests
{
    /// <summary>T043 (#83): «Товаров 3, отсканировано 2» — how much of the customer's order the scanner has found.</summary>
    public class ItemCountTests
    {
        private static readonly BoxId A = new BoxId("BOX-101");
        private static readonly BoxId B = new BoxId("BOX-102");
        private static readonly BoxId C = new BoxId("BOX-103");
        private static readonly BoxId Other = new BoxId("BOX-201");

        private static PickupDesk DeskWithAThreeItemOrder()
        {
            var desk = new PickupDesk(new BoxCatalog(new[]
            {
                new BoxRecord(A, "4401"), new BoxRecord(B, "4401"), new BoxRecord(C, "4401"), new BoxRecord(Other, "9163"),
            }));
            desk.OpenOrder(new Order("4401", new[] { A, B, C }), 0);
            return desk;
        }

        [Test]
        public void EachFoundItem_CountsOnce()
        {
            var desk = DeskWithAThreeItemOrder();
            var first = desk.Scan(A);
            Assert.AreEqual(ScanStatus.Matches, first.Status);
            Assert.AreEqual(3, first.ItemsInOrder);
            Assert.AreEqual(1, first.ItemsScanned);
            Assert.AreEqual(1, desk.Scan(A).ItemsScanned, "the same box scanned again is still one item");
            Assert.AreEqual(2, desk.Scan(B).ItemsScanned);
            Assert.AreEqual(2, desk.ActiveSession.ScannedCount);
        }

        [Test]
        public void AWrongBox_TellsTheCountSoFar_AndAddsNothing()
        {
            var desk = DeskWithAThreeItemOrder();
            desk.Scan(A);
            var wrong = desk.Scan(Other);
            Assert.AreEqual(ScanStatus.OtherOrder, wrong.Status);
            Assert.AreEqual(3, wrong.ItemsInOrder);
            Assert.AreEqual(1, wrong.ItemsScanned);
        }

        [Test]
        public void AnItemHandedOverWithoutAScan_StillCounts()
        {
            var desk = DeskWithAThreeItemOrder();
            desk.Scan(A);
            desk.Handover(A, 1);
            desk.Handover(B, 2); // never scanned
            Assert.AreEqual(2, desk.ActiveSession.ScannedCount, "the count never lags behind what is handed over");
            Assert.AreEqual(3, desk.Scan(C).ItemsScanned);
        }

        [Test]
        public void NoCustomer_NoCount()
        {
            var desk = new PickupDesk(new BoxCatalog(new[] { new BoxRecord(A, "4401") }));
            var scan = desk.Scan(A);
            Assert.AreEqual(ScanStatus.NoActiveOrder, scan.Status);
            Assert.AreEqual(0, scan.ItemsInOrder);
        }
    }
}
