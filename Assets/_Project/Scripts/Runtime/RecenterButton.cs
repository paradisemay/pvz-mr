using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Pvz.Runtime
{
    /// <summary>
    /// Start-menu button «Поставить стойку перед собой»: moves the whole play area (counter, boxes, customer path)
    /// so it stands in front of where the player is now. Useful when the headset placed it into furniture.
    /// </summary>
    [RequireComponent(typeof(PressableButton))]
    public sealed class RecenterButton : MonoBehaviour
    {
        [SerializeField] private MrSessionSetup session;
        [SerializeField] private CounterPlacementKeeper placement;

        private PressableButton _button;

        private void Awake() => _button = GetComponent<PressableButton>();
        private void OnEnable() => _button.Pressed += OnPressed;
        private void OnDisable() => _button.Pressed -= OnPressed;

        private void OnPressed() => Press();

        public void Press()
        {
            if (session != null) session.AlignPlayAreaToPlayer();
            if (placement != null) placement.ResetToDefault(); // the counter in front of the player is its new saved place (T034)
        }

        internal void Configure(MrSessionSetup mrSession, CounterPlacementKeeper counterPlacement = null)
        {
            session = mrSession;
            placement = counterPlacement;
        }
    }
}
