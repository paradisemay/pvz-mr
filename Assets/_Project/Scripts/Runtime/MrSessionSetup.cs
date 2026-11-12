using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// Switches between the two ways the game runs:
    /// on the headset — passthrough (transparent camera background), no mock room, no simulator;
    /// in the editor — a mock room so the scene can be played with the XR Interaction Simulator.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class MrSessionSetup : MonoBehaviour
    {
        [SerializeField] private Camera xrCamera;
        [SerializeField] private GameObject editorMockRoom;
        [SerializeField] private GameObject editorSimulator;
        [SerializeField] private Color editorBackground = new Color(0.16f, 0.18f, 0.22f, 1f);

        public static bool IsPassthroughDevice => !Application.isEditor;

        private void Awake()
        {
            var device = IsPassthroughDevice;
            if (editorMockRoom != null) editorMockRoom.SetActive(!device);
            if (device && editorSimulator != null) Destroy(editorSimulator);

            if (xrCamera != null)
            {
                xrCamera.clearFlags = CameraClearFlags.SolidColor;
                // Alpha 0 lets the Meta compositor show the passthrough feed behind rendered content.
                xrCamera.backgroundColor = device ? new Color(0f, 0f, 0f, 0f) : editorBackground;
            }
        }

        internal void Configure(Camera camera, GameObject mockRoom, GameObject simulator)
        {
            xrCamera = camera;
            editorMockRoom = mockRoom;
            editorSimulator = simulator;
        }
    }
}
