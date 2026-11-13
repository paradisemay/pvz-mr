using System.Globalization;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace Pvz.Core.Tests
{
    /// <summary>T017: the player's arrangement of the warehouse survives between sessions.</summary>
    public class WarehouseLayoutTests
    {
        [Test]
        public void Layout_SurvivesSavingAndLoading()
        {
            var store = new WarehouseLayoutStore(new MemoryTextStore());
            var layout = WarehouseLayout.Empty;
            layout.Set(new BoxPlacement(TestData.Box1, 1.25f, 0.13f, -2.5f, 90f));
            layout.Set(new BoxPlacement(TestData.Box2, -0.5f, 0.87f, 0.333f, -45.5f));
            store.Save(layout);

            var loaded = store.Load();
            Assert.AreEqual(2, loaded.Count);
            Assert.IsTrue(loaded.TryGet(TestData.Box2, out var p));
            Assert.AreEqual(-0.5f, p.X, 1e-3);
            Assert.AreEqual(0.87f, p.Y, 1e-3);
            Assert.AreEqual(0.333f, p.Z, 1e-3);
            Assert.AreEqual(-45.5f, p.Yaw, 1e-3);
        }

        [Test]
        public void NothingSaved_IsAnEmptyLayout()
        {
            Assert.AreEqual(0, new WarehouseLayoutStore(new MemoryTextStore()).Load().Count);
        }

        [Test]
        public void Encoding_DoesNotDependOnTheDeviceLanguage()
        {
            var culture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("ru-RU"); // decimal comma
                var layout = WarehouseLayout.Empty;
                layout.Set(new BoxPlacement(TestData.Box1, 1.5f, 0.25f, -0.75f, 10f));
                var text = layout.Encode();
                StringAssert.Contains("1.5;0.25;-0.75", text);
                Assert.IsTrue(WarehouseLayout.Decode(text).TryGet(TestData.Box1, out var p));
                Assert.AreEqual(1.5f, p.X, 1e-3);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = culture;
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("garbage")]
        [TestCase("v2|BOX-001;1;0;1;0")]
        [TestCase("v1|BOX-001;1;0")]
        [TestCase("v1|;1;0;1;0")]
        [TestCase("v1|BOX-001;a;b;c;d")]
        public void DamagedData_IsIgnored_WithoutErrors(string text)
        {
            Assert.AreEqual(0, WarehouseLayout.Decode(text).Count);
        }

        [Test]
        public void GoodEntries_SurviveNextToBrokenOnes()
        {
            var layout = WarehouseLayout.Decode("v1|BOX-001;oops|BOX-002;0.5;0.1;1;0|BOX-003;99;0;0;0");
            Assert.AreEqual(new[] { TestData.Box2 }, layout.Placements.Select(p => p.Box).ToArray(),
                "BOX-001 is broken, BOX-003 is 99 m away");
        }

        [TestCase(0f, -0.5f, 0f, false, TestName = "Under the floor")]
        [TestCase(0f, 0f, 0f, false, TestName = "Centre in the floor")]
        [TestCase(0f, 3.5f, 0f, false, TestName = "Above the ceiling")]
        [TestCase(5f, 0.1f, 5f, false, TestName = "Out of reach")]
        [TestCase(1f, 0.1f, -1f, true, TestName = "On the floor nearby")]
        [TestCase(float.NaN, 0.1f, 0f, false, TestName = "Not a number")]
        public void OnlyPlausiblePlaces_AreKept(float x, float y, float z, bool kept)
        {
            Assert.AreEqual(kept, WarehouseLayout.Empty.Set(new BoxPlacement(TestData.Box1, x, y, z, 0f)));
        }

        [Test]
        public void TheLastPlace_Wins()
        {
            var layout = WarehouseLayout.Empty;
            layout.Set(new BoxPlacement(TestData.Box1, 1f, 0.13f, 1f, 0f));
            layout.Set(new BoxPlacement(TestData.Box1, -1f, 0.13f, -1f, 0f));
            Assert.AreEqual(1, layout.Count);
            Assert.IsTrue(layout.TryGet(TestData.Box1, out var p));
            Assert.AreEqual(-1f, p.X);
        }
    }
}
