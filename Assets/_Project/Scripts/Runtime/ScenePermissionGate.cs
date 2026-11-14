using UnityEngine;
using UnityEngine.XR.ARFoundation;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace Pvz.Runtime
{
    /// <summary>
    /// Meta's OpenXR runtime only gives room data (planes, the room mesh) to apps that hold the Android permission
    /// <c>com.oculus.permission.USE_SCENE</c>. The <see cref="ARPlaneManager"/> is saved disabled in the scene
    /// and enabled here once the player grants it (or right away outside Android).
    /// Without the permission the game still works: boxes rest on the invisible floor, the ceiling height falls back to settings.
    /// </summary>
    public sealed class ScenePermissionGate : MonoBehaviour
    {
        public const string ScenePermission = "com.oculus.permission.USE_SCENE";

        [SerializeField] private ARPlaneManager planes;
        [Tooltip("The room mesh (T041): the same permission, enabled with the planes.")]
        [SerializeField] private ARMeshManager meshes;

        public bool Granted { get; private set; }

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
#else
            // Editor / Quest Link: no Android permission system.
            OnGranted(ScenePermission);
#endif
        }

        private void OnGranted(string permission)
        {
            Granted = true;
            if (planes != null) planes.enabled = true;
            if (meshes != null) meshes.enabled = true;
        }

        private void OnDenied(string permission)
        {
            Debug.LogWarning("[ScenePermissionGate] USE_SCENE denied: playing without room planes (floor only).");
        }

        internal void Configure(ARPlaneManager planeManager, ARMeshManager meshManager = null)
        {
            planes = planeManager;
            meshes = meshManager;
        }
    }
}
