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
        public void TheMissingText_TellsWhatToDo() =>
            StringAssert.Contains("отсканируйте", RoomMesh.MissingText);
    }
}
