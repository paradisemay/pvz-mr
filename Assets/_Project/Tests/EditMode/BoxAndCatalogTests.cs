using System;
using NUnit.Framework;

namespace Pvz.Core.Tests
{
    public class BoxAndCatalogTests
    {
        [Test]
        public void BoxId_IsNormalised()
        {
            Assert.AreEqual(new BoxId("BOX-001"), new BoxId("  box-001 "));
            Assert.AreEqual("BOX-001", new BoxId("box-001").Value);
        }

        [Test]
        public void BoxId_RejectsEmpty()
        {
            Assert.Throws<ArgumentException>(() => new BoxId(" "));
            Assert.IsFalse(BoxId.TryParse(null, out _));
            Assert.IsTrue(default(BoxId).IsEmpty);
        }

        [Test]
        public void Catalog_AddFindRemove()
        {
            var catalog = TestData.MvpCatalog();
            Assert.AreEqual(3, catalog.Count);
            Assert.IsTrue(catalog.TryGet(TestData.Box2, out var record));
            Assert.AreEqual("2781", record.OrderNumber);

            Assert.IsTrue(catalog.Remove(TestData.Box2));
            Assert.IsFalse(catalog.Contains(TestData.Box2));
            Assert.IsFalse(catalog.Remove(TestData.Box2));
            Assert.AreEqual(2, catalog.Count);
        }

        [Test]
        public void Catalog_RejectsDuplicates()
        {
            var catalog = TestData.MvpCatalog();
            Assert.Throws<InvalidOperationException>(() => catalog.Add(new BoxRecord(TestData.Box1, "9999")));
        }

        [Test]
        public void Order_RequiresAtLeastOneBox()
        {
            Assert.Throws<ArgumentException>(() => new Order("1", Array.Empty<BoxId>()));
            Assert.Throws<ArgumentException>(() => new Order(" ", new[] { TestData.Box1 }));
        }

        [Test]
        public void Order_IgnoresDuplicateBoxes()
        {
            var order = new Order("1435", new[] { TestData.Box1, TestData.Box1 });
            Assert.AreEqual(1, order.Boxes.Count);
        }

        [Test]
        public void Generator_PicksBoxByRandomIndex()
        {
            var generator = new OrderGenerator(new FakeRandom(2));
            var order = generator.CreateRandomOrder(TestData.MvpCatalog());
            Assert.AreEqual("5812", order.Number);
            CollectionAssert.AreEqual(new[] { TestData.Box3 }, order.Boxes);
        }

        [Test]
        public void Generator_GroupsBoxesOfSameOrder()
        {
            var catalog = TestData.MvpCatalog();
            var extra = new BoxId("BOX-004");
            catalog.Add(new BoxRecord(extra, "1435"));

            var order = new OrderGenerator(new FakeRandom(0)).CreateRandomOrder(catalog);

            Assert.AreEqual("1435", order.Number);
            CollectionAssert.AreEquivalent(new[] { TestData.Box1, extra }, order.Boxes);
        }

        [Test]
        public void Generator_ReturnsNullForEmptyCatalog()
        {
            Assert.IsNull(new OrderGenerator(new FakeRandom()).CreateRandomOrder(new BoxCatalog()));
        }

        [Test]
        public void Generator_NeverPicksOutsideCatalog()
        {
            var generator = new OrderGenerator(new SystemRandomSource(42));
            var catalog = TestData.MvpCatalog();
            for (var i = 0; i < 100; i++)
            {
                var order = generator.CreateRandomOrder(catalog);
                Assert.IsTrue(catalog.Contains(order.Boxes[0]));
            }
        }
    }
}
