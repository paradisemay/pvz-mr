using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Pvz.Runtime
{
    /// <summary>
    /// The room's mesh from Meta's Space Setup (T041, #82): the real shape of the bed, the table, the sofa, as invisible
    /// colliders — boxes falling out of the hatch land on the real furniture and bounce off it, not only off the room
    /// planes. The <see cref="ARMeshManager"/> is saved disabled and enabled by <see cref="ScenePermissionGate"/> with
    /// the planes (the same USE_SCENE permission). When the headset gives no mesh (the room was never scanned), the
    /// computer says so; the game goes on with the room planes and the invisible floor.
    /// </summary>
    public sealed class RoomMesh : MonoBehaviour
    {
        public enum State
        {
            /// <summary>No meshing here (the editor's mock room, Quest Link, no permission): nothing to say.</summary>
            Off,
            /// <summary>Meshing runs, the mesh has not come yet.</summary>
            Waiting,
            /// <summary>The room's mesh is in: boxes collide with the real furniture.</summary>
            Ready,
            /// <summary>Meshing runs but no mesh came: the room was not scanned on the headset.</summary>
            Missing,
        }

        public const string MissingText = "Мебель не найдена: отсканируйте\nкомнату в настройках шлема.";

        [SerializeField] private ARMeshManager meshes;
        [Tooltip("Meshing runs this long without a mesh: the room was not scanned.")]
        [SerializeField, Min(1f)] private float waitSeconds = 8f;

        private float _runningSince = float.NaN;

        /// <summary>
        /// The layer of the room mesh's colliders. The game's own checks (the bag spot, the scan ray) skip it: invisible
        /// triangles of a real table next to the counter must not block a bag or a scan.
        /// </summary>
        public const string LayerName = "RoomMesh";

        /// <summary><paramref name="mask"/> without the room mesh's layer.</summary>
        public static int Without(int mask)
        {
            var layer = LayerMask.NameToLayer(LayerName);
            return layer < 0 ? mask : mask & ~(1 << layer);
        }

        public State Current { get; private set; } = State.Off;

        /// <summary>
        /// Chunks that really became colliders: the manager lists a chunk before its mesh is generated (and keeps one
        /// whose generation failed), and keeps its chunks as colliders after it is disabled.
        /// </summary>
        public int MeshCount
        {
            get
            {
                if (meshes == null || meshes.meshes == null) return 0;
                var count = 0;
                foreach (var chunk in meshes.meshes)
                    if (chunk != null && chunk.gameObject.activeSelf && chunk.sharedMesh != null) count++;
                return count;
            }
        }
        public bool IsMissing => Current == State.Missing;

        public event Action<State> Changed;

        private void Update()
        {
            var running = meshes != null && meshes.enabled && meshes.subsystem != null && meshes.subsystem.running;
            if (running && float.IsNaN(_runningSince)) _runningSince = Time.unscaledTime;
            if (!running) _runningSince = float.NaN;
            Report(Evaluate(running, MeshCount, running ? Time.unscaledTime - _runningSince : 0f, waitSeconds));
        }

        internal void Report(State next)
        {
            if (next == Current) return;
            Current = next;
            Changed?.Invoke(next);
        }

        /// <summary>The state from what the headset gives: pure, so it is tested without a headset.</summary>
        public static State Evaluate(bool running, int meshCount, float runningSeconds, float waitSeconds)
        {
            if (!running) return State.Off;
            if (meshCount > 0) return State.Ready;
            return runningSeconds >= waitSeconds ? State.Missing : State.Waiting;
        }

        internal void Configure(ARMeshManager meshManager) => meshes = meshManager;
    }
}
