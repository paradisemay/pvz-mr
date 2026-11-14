using System.Linq;
using NUnit.Framework;

namespace Pvz.Core.Tests
{
    /// <summary>#106: an order that comes with the delivery is offered only once all its boxes are received.</summary>
    public class PartialDeliveryTests
    {
        private static readonly BoxRecord[] BigOrder =
        {
            new BoxRecord(new BoxId("BOX-101"), "3307"),
            new BoxRecord(new BoxId("BOX-102"), "3307"),
            new BoxRecord(new BoxId("BOX-104"), "3307"),
        };

        [Test]
        public void APartlyReceivedOrder_IsNotOffered_UntilAllItsBoxesAreIn()
        {
            var shift = new Shift(new BoxCatalog(new[] { new BoxRecord(TestData.Box1, "1435") }), new SystemRandomSource(1))
            {
                QueueLength = 4,
            };
            shift.ExpectDelivery(BigOrder);
            shift.Scan(BigOrder[0].Id); // one of three received

            Assert.IsTrue(shift.TryEnqueue(out var first));
            Assert.AreEqual("1435", first.Order.Number);
            Assert.IsFalse(shift.HasUnreservedOrders, "3307 is not ready: two of its boxes are still in the delivery");
            Assert.IsFalse(shift.TryEnqueue(out _));

            shift.Scan(BigOrder[1].Id);
            shift.Scan(BigOrder[2].Id);
            Assert.IsTrue(shift.TryEnqueue(out var big), "all three in: the order is ready");
            Assert.AreEqual("3307", big.Order.Number);
            CollectionAssert.AreEquivalent(BigOrder.Select(b => b.Id), big.Order.Boxes, "the customer gets the whole order");
        }
    }
}
