using NUnit.Framework;

namespace Pvz.Core.Tests
{
    /// <summary>T035 (#79): the customer shows a barcode on their phone; nothing is served before it is scanned.</summary>
    public class CustomerCodeTests
    {
        private static PickupDesk Desk() => new PickupDesk(TestData.MvpCatalog()) { RequiresCustomerCode = true };

        private static Order OrderOf(PickupDesk desk, string number)
        {
            var boxes = new System.Collections.Generic.List<BoxId>();
            foreach (var box in desk.Catalog.Boxes) if (box.OrderNumber == number) boxes.Add(box.Id);
            return new Order(number, boxes);
        }

        [Test]
        public void BeforeTheCode_BoxesAreNotJudged_NorHandedOver_AndItIsNoMistake()
        {
            var desk = Desk();
            var order = OrderOf(desk, "1435");
            desk.OpenOrder(order, 0);
            Assert.IsTrue(desk.AwaitsCode);
            var mistakes = 0;
            desk.WrongHandover += _ => mistakes++;

            Assert.AreEqual(ScanStatus.CodeNotScanned, desk.Scan(order.Boxes[0]).Status, "the box is not named before the code");
            Assert.AreEqual(ScanStatus.CodeNotScanned, desk.Scan(TestData.Box2).Status, "nor is another order's box");
            Assert.AreEqual(HandoverStatus.CodeNotScanned, desk.Handover(order.Boxes[0], 1).Status);
            Assert.AreEqual(BagStatus.CodeNotScanned, desk.HandOverBag(1));
            Assert.AreEqual(0, mistakes);
            Assert.IsTrue(desk.Catalog.Contains(order.Boxes[0]), "the box stays in the warehouse");
        }

        [Test]
        public void ScanningTheCode_OpensTheService_Once()
        {
            var desk = Desk();
            var order = OrderOf(desk, "1435");
            var raised = 0;
            desk.CodeScanned += () => raised++;
            Assert.IsFalse(desk.ScanCustomerCode(), "nobody at the counter");
            desk.OpenOrder(order, 0);
            Assert.IsTrue(desk.ScanCustomerCode());
            Assert.IsFalse(desk.ScanCustomerCode(), "the code is scanned once");
            Assert.AreEqual(1, raised);

            Assert.AreEqual(ScanStatus.Matches, desk.Scan(order.Boxes[0]).Status);
            Assert.AreEqual(HandoverStatus.Accepted, desk.Handover(order.Boxes[0], 2).Status);
        }

        [Test]
        public void TheNextCustomer_ShowsTheirOwnCode()
        {
            var desk = Desk();
            var first = OrderOf(desk, "1435");
            desk.OpenOrder(first, 0);
            desk.ScanCustomerCode();
            desk.Handover(first.Boxes[0], 1);
            Assert.IsFalse(desk.HasCustomer);

            desk.OpenOrder(OrderOf(desk, "2781"), 2);
            Assert.IsTrue(desk.AwaitsCode, "a new customer, a new code");
        }

        [Test]
        public void AReturn_AlsoWaitsForTheCode()
        {
            var desk = Desk();
            var request = new ReturnRequest(new BoxId("BOX-900"), "9999");
            desk.OpenReturn(request);
            Assert.AreEqual(ScanStatus.CodeNotScanned, desk.Scan(request.Box).Status);
            Assert.IsTrue(desk.HasActiveReturn, "not accepted yet");
            desk.ScanCustomerCode();
            Assert.AreEqual(ScanStatus.ReturnAccepted, desk.Scan(request.Box).Status);
        }

        [Test]
        public void WithoutTheRule_EveryoneIsServedAtOnce()
        {
            var desk = new PickupDesk(TestData.MvpCatalog());
            var order = OrderOf(desk, "1435");
            desk.OpenOrder(order, 0);
            Assert.IsFalse(desk.AwaitsCode);
            Assert.AreEqual(HandoverStatus.Accepted, desk.Handover(order.Boxes[0], 1).Status);
        }

        [Test]
        public void Patience_CountsFromTheArrival_NotFromTheCode()
        {
            using (var shift = new Shift(TestData.MvpCatalog(), new SystemRandomSource(3)) { PatienceSeconds = 10, RequiresCustomerCode = true })
            {
                Assert.IsTrue(shift.TryEnqueue(out _));
                shift.OpenNext(0);
                Order left = null;
                shift.OrderAbandoned += o => left = o;
                shift.Tick(10);
                Assert.IsNotNull(left, "a customer whose code nobody scans leaves too");
            }
        }
    }
}
