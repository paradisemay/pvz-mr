using System.Collections;
using UnityEngine;
using UnityEngine.XR;

namespace Pvz.Runtime
{
    /// <summary>
    /// Switches between the two ways the game runs:
    /// with a headset (device build or Quest Link) — passthrough, real room, no simulator, no stick locomotion,
    /// and the play area is aligned to where the player actually stands;
    /// without a headset (editor) — mock room and XR Interaction Simulator.
    /// The simulator and mock room are tagged EditorOnly by the builder, so they are stripped from device builds;
    /// the simulator is saved inactive because, once enabled, it replaces the real HMD in the Input System.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class MrSessionSetup : MonoBehaviour
    {
        [SerializeField] private Camera xrCamera;
        [SerializeField] private GameObject editorMockRoom;
        [SerializeField] private GameObject editorSimulator;
        [SerializeField] private GameObject locomotion;
        [SerializeField] private Transform playArea;
        [SerializeField] private Color editorBackground = new Color(0.16f, 0.18f, 0.22f, 1f);
        [SerializeField, Min(0)] private float alignAfterSeconds = 0.5f;

        /// <summary>True when a real headset drives the camera.</summary>
        public static bool HasHeadset => XRSettings.isDeviceActive;

        private void Awake()
        {
            var headset = HasHeadset;
            if (editorMockRoom != null) editorMockRoom.SetActive(!headset);
            if (editorSimulator != null) editorSimulator.SetActive(!headset && Application.isEditor);
            // Moving virtual content with the sticks would detach it from the real room.
            if (headset && locomotion != null) locomotion.SetActive(false);

            if (xrCamera != null)
            {
                xrCamera.clearFlags = CameraClearFlags.SolidColor;
                // Alpha 0 lets the Meta compositor show the passthrough feed behind rendered content.
                xrCamera.backgroundColor = headset ? new Color(0f, 0f, 0f, 0f) : editorBackground;
            }
        }

        private IEnumerator Start()
        {
            if (!HasHeadset || playArea == null || xrCamera == null) yield break;
            // Wait for head tracking to report a real pose, then put the counter in front of the player.
            yield return new WaitForSeconds(alignAfterSeconds);
            AlignPlayAreaToPlayer();
        }

        /// <summary>Moves the play area so its origin is under the player's head and +Z is where they look.</summary>
        public void AlignPlayAreaToPlayer()
        {
            var head = xrCamera.transform;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            var position = new Vector3(head.position.x, playArea.position.y, head.position.z);
            playArea.SetPositionAndRotation(position, Quaternion.LookRotation(forward.normalized, Vector3.up));
            Physics.SyncTransforms();
        }

        internal void Configure(Camera camera, GameObject mockRoom, GameObject simulator, GameObject locomotionRoot, Transform playAreaRoot)
        {
            xrCamera = camera;
            editorMockRoom = mockRoom;
            editorSimulator = simulator;
            locomotion = locomotionRoot;
            playArea = playAreaRoot;
        }
    }
}
