using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Pvz.Runtime
{
    /// <summary>Start-menu button «ТЕМА»: switches the counter to the next theme the career has opened (T022).</summary>
    [RequireComponent(typeof(PressableButton))]
    public sealed class ThemeButton : MonoBehaviour
    {
        [SerializeField] private CareerKeeper keeper;
        [SerializeField] private TMP_Text label;

        private PressableButton _button;

        private void Awake() => _button = GetComponent<PressableButton>();
        private void OnEnable() => _button.Pressed += OnPressed;
        private void OnDisable() => _button.Pressed -= OnPressed;

        private void OnPressed() => Press();

        public void Press()
        {
            if (keeper != null) keeper.NextTheme();
        }

        public string Label => label != null ? label.text : string.Empty;

        public void ShowLabel(string text)
        {
            if (label != null) label.text = text;
        }

        internal void Configure(CareerKeeper careerKeeper, TMP_Text labelText)
        {
            keeper = careerKeeper;
            label = labelText;
        }
    }
}
