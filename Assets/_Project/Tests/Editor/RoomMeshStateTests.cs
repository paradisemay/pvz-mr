using NUnit.Framework;
using Pvz.Runtime;

namespace Pvz.Editor.Tests
{
    /// <summary>T041 (#82): what the computer says about the room mesh, from what the headset gives.</summary>
    public class RoomMeshStateTests
    {
        [Test]
        public void NoMeshing_SaysNothing() =>
            Assert.AreEqual(RoomMesh.State.Off, RoomMesh.Evaluate(running: false, meshCount: 0, runningSeconds: 30f, waitSeconds: 8f));

        [Test]
        public void MeshingWithoutAMeshYet_Waits() =>
            Assert.AreEqual(RoomMesh.State.Waiting, RoomMesh.Evaluate(true, 0, 3f, 8f));

        [Test]
        public void AMesh_IsReady_HoweverLongItTook() =>
            Assert.AreEqual(RoomMesh.State.Ready, RoomMesh.Evaluate(true, 4, 30f, 8f));

        [Test]
        public void NoMeshAfterTheWait_TheRoomWasNotScanned() =>
            Assert.AreEqual(RoomMesh.State.Missing, RoomMesh.Evaluate(true, 0, 8f, 8f));

        [Test]
        public void TheGamesOwnChecks_SkipTheRoomMeshLayer_AndOnlyIt()
        {
            var layer = UnityEngine.LayerMask.NameToLayer(RoomMesh.LayerName);
            Assert.GreaterOrEqual(layer, 0, "the layer exists in the project");
            var mask = RoomMesh.Without(~0);
            Assert.AreEqual(0, mask & (1 << layer));
            Assert.AreEqual(~0 & ~(1 << layer), mask, "every other layer is still checked");
        }

        [Test]
        public void TheChunkPrefab_IsOnTheRoomMeshLayer()
        {
            var chunk = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/RoomMeshChunk.prefab");
            Assert.AreEqual(UnityEngine.LayerMask.NameToLayer(RoomMesh.LayerName), chunk.layer);
        }

        [Test]
        public void TheMissingText_TellsWhatToDo() =>
            StringAssert.Contains("отсканируйте", RoomMesh.MissingText);
    }
}
