using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Pvz.Runtime
{
    /// <summary>
    /// «Стойку ко мне» (on the computer while moving the counter, before the shift): puts the counter in front of where the
    /// player is now and saves that place. Useful when the counter ended up in furniture or out of reach.
    /// </summary>
    [RequireComponent(typeof(PressableButton))]
    public sealed class RecenterButton : MonoBehaviour
    {
        [SerializeField] private MrSessionSetup session;
        [SerializeField] private PlacementKeeper placement;
        [SerializeField] private PlacementMover mover;

        private PressableButton _button;

        private void Awake() => _button = GetComponent<PressableButton>();
        private void OnEnable() => _button.Pressed += OnPressed;
        private void OnDisable() => _button.Pressed -= OnPressed;

        private void OnPressed() => Press();

        public void Press()
        {
            // T037: only the counter comes to the player (the hatch and the rack keep their places in the room); saved.
            var head = Camera.main != null ? Camera.main.transform : null;
            if (mover != null && mover.IsMoving && head != null)
            {
                // While the counter is being moved: «Применить» saves it, «Отмена» puts it back (T037 review).
                var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
                mover.PlaceWhileMoving(head.position, Quaternion.LookRotation(forward.normalized, Vector3.up));
            }
            else if (placement != null && head != null) placement.PutInFrontOf(head);
            else if (session != null) session.AlignPlayAreaToPlayer();
        }

        internal void Configure(MrSessionSetup mrSession, PlacementKeeper counterPlacement = null, PlacementMover counterMover = null)
        {
            session = mrSession;
            placement = counterPlacement;
            mover = counterMover;
        }
    }
}
