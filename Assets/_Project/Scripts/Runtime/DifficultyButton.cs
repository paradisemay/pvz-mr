using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Pvz.Runtime
{
    /// <summary>A button on the counter that starts the shift with its difficulty.</summary>
    [RequireComponent(typeof(PressableButton))]
    public sealed class DifficultyButton : MonoBehaviour
    {
        [SerializeField] private ShiftSettings settings;

        private PressableButton _button;

        public event Action<ShiftSettings> Chosen;

        public ShiftSettings Settings => settings;

        private void Awake() => _button = GetComponent<PressableButton>();
        private void OnEnable() => _button.Pressed += OnPressed;
        private void OnDisable() => _button.Pressed -= OnPressed;

        private void OnPressed() => Press();

        /// <summary>Same as pressing the button with a controller (tests, debug).</summary>
        public void Press() => Chosen?.Invoke(settings);

        internal void Configure(ShiftSettings shiftSettings) => settings = shiftSettings;
    }
}
