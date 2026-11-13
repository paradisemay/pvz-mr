using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Pvz.Runtime
{
    /// <summary>Start-menu button «ТЕМА»: switches the counter to the next theme the career has opened (T022).</summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class ThemeButton : MonoBehaviour
    {
        [SerializeField] private ShiftDirector director;
        [SerializeField] private TMP_Text label;

        private XRSimpleInteractable _interactable;

        private void Awake() => _interactable = GetComponent<XRSimpleInteractable>();
        private void OnEnable() => _interactable.selectExited.AddListener(OnReleased);
        private void OnDisable() => _interactable.selectExited.RemoveListener(OnReleased);

        private void OnReleased(SelectExitEventArgs args)
        {
            if (!args.isCanceled) Press();
        }

        public void Press()
        {
            if (director != null) director.NextTheme();
        }

        public string Label => label != null ? label.text : string.Empty;

        public void ShowLabel(string text)
        {
            if (label != null) label.text = text;
        }

        internal void Configure(ShiftDirector shiftDirector, TMP_Text labelText)
        {
            director = shiftDirector;
            label = labelText;
        }
    }
}
