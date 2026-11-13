using UnityEngine;
using UnityEngine.XR.ARFoundation;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Pvz.Runtime
{
    /// <summary>
    /// Meta's OpenXR runtime only gives room data (planes) to apps that hold the Android permission
    /// <c>com.oculus.permission.USE_SCENE</c>. Plane detection stays off until the player grants it;
    /// without it the game still works (boxes rest on the invisible floor), just without real tables and walls.
    /// </summary>
    public sealed class ScenePermissionGate : MonoBehaviour
    {
        public const string ScenePermission = "com.oculus.permission.USE_SCENE";

        [SerializeField] private ARPlaneManager planes;

        public bool Granted { get; private set; }

        private void Awake()
        {
            if (Application.isEditor || Application.platform != RuntimePlatform.Android)
            {
                Granted = true; // no permission system in the editor
                return;
            }
            if (planes != null) planes.enabled = false;
        }

        private void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permission.HasUserAuthorizedPermission(ScenePermission))
            {
                OnGranted(ScenePermission);
                return;
            }
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += OnGranted;
            callbacks.PermissionDenied += OnDenied;
            Permission.RequestUserPermission(ScenePermission, callbacks);
#endif
        }

        private void OnGranted(string permission)
        {
            Granted = true;
            if (planes != null) planes.enabled = true;
        }

        private void OnDenied(string permission)
        {
            Debug.LogWarning("[ScenePermissionGate] USE_SCENE denied: playing without room planes (floor only).");
        }

        internal void Configure(ARPlaneManager planeManager) => planes = planeManager;
    }
}
