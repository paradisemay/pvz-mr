using NUnit.Framework;

namespace Pvz.Core.Tests
{
    /// <summary>T016: «похожие заказы» — numbers easy to mix up.</summary>
    public class LookAlikeOrderTests
    {
        [TestCase("2781", "2718", true, TestName = "Neighbours swapped")]
        [TestCase("3307", "3370", true, TestName = "Neighbours swapped (repeated digit)")]
        [TestCase("5812", "5912", true, TestName = "One digit different")]
        [TestCase("3307", "3377", true, TestName = "One digit different (repeat)")]
        [TestCase("1435", "2781", false, TestName = "Unrelated")]
        [TestCase("2781", "2781", false, TestName = "The same number is not a look-alike")]
        [TestCase("281", "2781", false, TestName = "Different length")]
        [TestCase("1435", "5134", false, TestName = "Same digits but all shuffled")]
        [TestCase("2781", "8721", false, TestName = "Far digits swapped")]
        [TestCase("3307", "4417", false, TestName = "Three digits different")]
        [TestCase("12", "13", false, TestName = "Too short to compare")]
        [TestCase("123", "124", true, TestName = "Shortest comparable")]
        public void LookAlike(string a, string b, bool expected)
        {
            Assert.AreEqual(expected, OrderNumbers.LookAlike(a, b));
            Assert.AreEqual(expected, OrderNumbers.LookAlike(b, a), "symmetric");
        }

        [Test]
        public void Null_IsNeverALookAlike() => Assert.IsFalse(OrderNumbers.LookAlike(null, "2781"));

        private static PickupDesk DeskWithLookAlikes() => new PickupDesk(new BoxCatalog(new[]
        {
            new BoxRecord(TestData.Box1, "2781"),
            new BoxRecord(TestData.Box2, "2718"),
            new BoxRecord(TestData.Box3, "1435"),
        }));

        [Test]
        public void ScanningALookAlikeBox_WarnsThePlayer()
        {
            var desk = DeskWithLookAlikes();
            desk.OpenOrder(TestData.OrderFor(TestData.Box1, "2781"), 0);

            var lookAlike = desk.Scan(TestData.Box2);
            Assert.AreEqual(ScanStatus.OtherOrder, lookAlike.Status);
            Assert.IsTrue(lookAlike.LooksLikeTheWantedOrder);

            var unrelated = desk.Scan(TestData.Box3);
            Assert.AreEqual(ScanStatus.OtherOrder, unrelated.Status);
            Assert.IsFalse(unrelated.LooksLikeTheWantedOrder);

            Assert.IsFalse(desk.Scan(TestData.Box1).LooksLikeTheWantedOrder, "the right box is not a trap");
        }

        [Test]
        public void HandingOverALookAlikeBox_IsAWrongBox_ThatTheCustomerRecognises()
        {
            var desk = DeskWithLookAlikes();
            desk.OpenOrder(TestData.OrderFor(TestData.Box1, "2781"), 0);
            var outcome = desk.Handover(TestData.Box2, 1);
            Assert.AreEqual(HandoverStatus.WrongBox, outcome.Status);
            Assert.IsTrue(outcome.LooksLikeTheWantedOrder);
            Assert.IsFalse(desk.Handover(TestData.Box3, 2).LooksLikeTheWantedOrder);
        }

        [Test]
        public void TheFlag_IsOnlyForWrongBoxes()
        {
            Assert.IsFalse(new ScanOutcome(ScanStatus.Matches, TestData.Box1, "2781", true).LooksLikeTheWantedOrder);
            Assert.IsFalse(new HandoverOutcome(HandoverStatus.Accepted, TestData.Box1, true, looksLikeTheWantedOrder: true).LooksLikeTheWantedOrder);
        }
    }
}
