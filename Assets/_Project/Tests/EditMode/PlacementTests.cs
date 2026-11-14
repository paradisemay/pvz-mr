using System;
using NUnit.Framework;

namespace Pvz.Core.Tests
{
    /// <summary>T034 (#78): where the counter stands in the room, kept between sessions.</summary>
    public class PlacementTests
    {
        [Test]
        public void SavedPlacement_IsLoadedBackExactly()
        {
            var store = new PlacementStore(new MemoryTextStore());
            Assert.IsFalse(store.TryLoad(out _), "nothing saved yet");
            var placement = new Placement(1.234567f, -0.5f, 93.25f);
            store.Save(placement);
            Assert.IsTrue(store.TryLoad(out var loaded));
            Assert.AreEqual(placement, loaded);
        }

        [Test]
        public void Yaw_IsKeptWithinAFullTurn()
        {
            Assert.AreEqual(270f, new Placement(0, 0, -90f).Yaw, 1e-4);
            Assert.AreEqual(10f, new Placement(0, 0, 730f).Yaw, 1e-4);
        }

        [Test]
        public void DamagedOrForeignText_IsNoPlacement()
        {
            foreach (var text in new[] { null, "", "1;2", "a;b;c", "1;2;3;4", "1,5;2;3", "NaN;0;0", "1;Infinity;0" })
                Assert.IsFalse(Placement.TryParse(text, out _), text ?? "null");
        }

        [Test]
        public void Forget_ClearsTheSavedPlacement()
        {
            var store = new PlacementStore(new MemoryTextStore());
            store.Save(new Placement(1, 2, 3));
            store.Forget();
            Assert.IsFalse(store.TryLoad(out _));
        }

        [Test]
        public void NotANumber_IsRejected() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new Placement(float.NaN, 0, 0));
    }
}
