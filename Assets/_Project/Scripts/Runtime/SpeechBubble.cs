using TMPro;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>World-space text bubble above a customer, always facing the player's camera.</summary>
    public sealed class SpeechBubble : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text label;

        private Camera _camera;

        public string Text => label != null && root != null && root.activeSelf ? label.text : string.Empty;

        public void Show(string text)
        {
            if (label != null) label.text = text;
            if (root != null) root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            var away = transform.position - _camera.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }

        internal void Configure(GameObject bubbleRoot, TMP_Text text)
        {
            root = bubbleRoot;
            label = text;
        }
    }
}
