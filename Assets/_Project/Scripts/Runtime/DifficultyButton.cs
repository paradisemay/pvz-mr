using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Pvz.Runtime
{
    /// <summary>A button on the counter that starts the shift with its difficulty.</summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class DifficultyButton : MonoBehaviour
    {
        [SerializeField] private ShiftSettings settings;

        private XRSimpleInteractable _interactable;

        public event Action<ShiftSettings> Chosen;

        public ShiftSettings Settings => settings;

        private void Awake() => _interactable = GetComponent<XRSimpleInteractable>();
        private void OnEnable() => _interactable.selectExited.AddListener(OnReleased);
        private void OnDisable() => _interactable.selectExited.RemoveListener(OnReleased);

        private void OnReleased(SelectExitEventArgs args)
        {
            if (!args.isCanceled) Press();
        }

        /// <summary>Same as pressing the button with a controller (tests, debug).</summary>
        public void Press() => Chosen?.Invoke(settings);

        internal void Configure(ShiftSettings shiftSettings) => settings = shiftSettings;
    }
}
