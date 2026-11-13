using TMPro;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>World-space text bubble beside a customer; a <see cref="Billboard"/> keeps it facing the player.</summary>
    [RequireComponent(typeof(Billboard))]
    public sealed class SpeechBubble : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text label;

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

        internal void Configure(GameObject bubbleRoot, TMP_Text text)
        {
            root = bubbleRoot;
            label = text;
        }
    }
}
