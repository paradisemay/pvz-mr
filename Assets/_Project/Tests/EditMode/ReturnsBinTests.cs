using System.Linq;
using NUnit.Framework;

namespace Pvz.Core.Tests
{
    /// <summary>T045 (#85): refusals at the counter, the returns bin and handing the returns in.</summary>
    public class ReturnsBinTests
    {
        private static Shift ShiftWithOneOrder(int refusalChance, out Order order)
        {
            var shift = new Shift(TestData.MvpCatalog(), new SystemRandomSource(5)) { RefusalChancePercent = refusalChance };
            order = shift.NextOrder();
            shift.Desk.OpenOrder(order, 0);
            return shift;
        }

        private static void HandOverAll(Shift shift, Order order)
        {
            foreach (var box in order.Boxes) shift.Desk.Handover(box, 1);
            if (shift.Desk.HasActiveOrder && shift.Desk.ActiveSession.AwaitingBag) shift.Desk.HandOverBag(2);
        }

        [Test]
        public void ARefusal_CompletesTheOrder_AndTheBoxComesBackForTheBin()
        {
            var shift = ShiftWithOneOrder(100, out var order);
            BoxRecord refused = null;
            shift.BoxRefused += r => refused = r;
            HandOverAll(shift, order);

            Assert.AreEqual(1, shift.Stats.OrdersCompleted, "the order counts as handed over, with a refusal");
            Assert.IsNotNull(refused);
            CollectionAssert.Contains(order.Boxes.ToArray(), refused.Id);
            Assert.AreEqual(1, shift.Stats.Refusals);
            Assert.IsTrue(shift.WasRefused(order.Number));
            Assert.AreEqual(ReturnKind.Refusal, shift.Returns.KindOf(refused.Id));
            Assert.AreEqual(1, shift.Returns.Pending);
        }

        [Test]
        public void NoRefusalChance_NoRefusal()
        {
            var shift = ShiftWithOneOrder(0, out var order);
            HandOverAll(shift, order);
            Assert.AreEqual(0, shift.Stats.Refusals);
            Assert.AreEqual(0, shift.Returns.Pending);
        }

        [Test]
        public void AnAcceptedReturn_WaitsForTheBin()
        {
            var shift = new Shift(TestData.MvpCatalog(), new SystemRandomSource(1));
            var sold = new BoxId("BOX-801"); // handed over on another day: not in the warehouse
            shift.Desk.OpenReturn(new ReturnRequest(sold, "8801"));
            Assert.AreEqual(ScanStatus.ReturnAccepted, shift.Desk.Scan(sold).Status);
            Assert.AreEqual(ReturnKind.Return, shift.Returns.KindOf(sold));
        }

        [Test]
        public void HandingIn_TakesOnlyWhatIsInTheBin_ForPoints()
        {
            var shift = ShiftWithOneOrder(100, out var order);
            BoxRecord refused = null;
            shift.BoxRefused += r => refused = r;
            HandOverAll(shift, order);
            var before = shift.Stats.Score;

            Assert.IsEmpty(shift.HandInReturns(), "nothing in the bin yet");
            Assert.IsFalse(shift.Returns.Stow(new BoxId("BOX-999")), "not a return: it does not count");
            Assert.IsTrue(shift.Returns.Stow(refused.Id));
            Assert.IsTrue(shift.Returns.Unstow(refused.Id), "taken out again");
            Assert.AreEqual(0, shift.Returns.InBin);
            shift.Returns.Stow(refused.Id);
            CollectionAssert.AreEqual(new[] { refused.Id }, shift.HandInReturns());
            Assert.AreEqual(1, shift.Stats.ReturnsHandedIn);
            Assert.AreEqual(before + ScoringRules.Default.ReturnHandedIn, shift.Stats.Score);
            Assert.AreEqual(0, shift.Returns.Pending);
        }

        [Test]
        public void TheEndOfTheShift_HandsInTheBin_AndPenalisesWhatLiesElsewhere()
        {
            BoxId A = new BoxId("BOX-801"), B = new BoxId("BOX-802"); // sold on other days
            var shift = new Shift(TestData.MvpCatalog(), new SystemRandomSource(1));
            shift.Desk.OpenReturn(new ReturnRequest(A, "8801"));
            shift.Desk.Scan(A);
            shift.Desk.OpenReturn(new ReturnRequest(B, "8802"));
            shift.Desk.Scan(B);
            shift.Returns.Stow(A);
            var before = shift.Stats.Score;

            var (handed, lost) = shift.CloseReturns();
            CollectionAssert.AreEqual(new[] { A }, handed);
            CollectionAssert.AreEqual(new[] { B }, lost);
            Assert.AreEqual(1, shift.Stats.ReturnsHandedIn);
            Assert.AreEqual(1, shift.Stats.ReturnsLost);
            Assert.AreEqual(before + ScoringRules.Default.ReturnHandedIn - ScoringRules.Default.ReturnLostPenalty, shift.Stats.Score);
        }
    }
}
