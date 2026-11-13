using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>Keeps a world-space label upright and facing the player's camera (speech bubbles, patience bars).</summary>
    public sealed class Billboard : MonoBehaviour
    {
        private Camera _camera;

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            var away = transform.position - _camera.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }
    }
}
