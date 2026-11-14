using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Pvz.Runtime
{
    /// <summary>
    /// «Переместить стойку» (T034, #78): a handle appears by the counter; the player takes it with grip and carries the
    /// counter — it slides on the floor and turns around the vertical only. «Применить» keeps the new place (saved),
    /// «Отмена» puts the counter back. Works before and during a shift: the line walks to the new place.
    /// </summary>
    public sealed class CounterMover : MonoBehaviour
    {
        [SerializeField] private CounterPlacementKeeper keeper;
        [SerializeField] private XRGrabInteractable handle;
        [Tooltip("Where the handle sits relative to the counter rig when nobody holds it.")]
        [SerializeField] private Vector3 handleOffset = new Vector3(0f, 1.45f, -0.55f);

        private Pose _before;
        private float _rigHeight;

        public bool IsMoving { get; private set; }
        public bool IsHandleHeld => handle != null && handle.isSelected;
        public XRGrabInteractable Handle => handle;

        public event Action<bool> MovingChanged;

        private void Awake()
        {
            if (handle != null) handle.gameObject.SetActive(false);
        }

        public void Begin()
        {
            if (IsMoving || keeper == null || keeper.Rig == null) return;
            var rig = keeper.Rig;
            _before = new Pose(rig.position, rig.rotation);
            _rigHeight = rig.position.y;
            IsMoving = true;
            if (handle != null)
            {
                handle.gameObject.SetActive(true);
                SnapHandle();
            }
            MovingChanged?.Invoke(true);
        }

        /// <summary>Keeps the new place: saved, and the customers walk to it.</summary>
        public void Apply()
        {
            if (!IsMoving) return;
            Finish();
            keeper.SaveCurrent();
            keeper.AfterMove();
        }

        /// <summary>Puts the counter back where it was before «Переместить стойку».</summary>
        public void Cancel()
        {
            if (!IsMoving) return;
            Finish();
            keeper.Rig.SetPositionAndRotation(_before.position, _before.rotation);
            keeper.AfterMove();
        }

        private void Finish()
        {
            IsMoving = false;
            if (handle != null)
            {
                if (handle.isSelected && handle.interactionManager != null) handle.interactionManager.CancelInteractableSelection((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)handle);
                handle.gameObject.SetActive(false);
            }
            MovingChanged?.Invoke(false);
        }

        private void LateUpdate()
        {
            if (!IsMoving || handle == null) return;
            if (!handle.isSelected)
            {
                SnapHandle();
                return;
            }
            Follow(handle.transform.position, handle.transform.forward);
        }

        /// <summary>The counter follows a held handle: same offset, on the floor, turned only around the vertical.</summary>
        public void Follow(Vector3 handlePosition, Vector3 handleForward)
        {
            var rig = keeper.Rig;
            var forward = Vector3.ProjectOnPlane(handleForward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(rig.forward, Vector3.up);
            var rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            var position = handlePosition - rotation * handleOffset;
            position.y = _rigHeight;
            rig.SetPositionAndRotation(position, rotation);
            keeper.AfterMoveQuietly();
        }

        private void SnapHandle()
        {
            var rig = keeper.Rig;
            handle.transform.SetPositionAndRotation(rig.TransformPoint(handleOffset), rig.rotation);
            var body = handle.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position = handle.transform.position;
                body.rotation = handle.transform.rotation;
            }
        }

        internal void Configure(CounterPlacementKeeper placementKeeper, XRGrabInteractable grabHandle, Vector3 offset)
        {
            keeper = placementKeeper;
            handle = grabHandle;
            handleOffset = offset;
        }
    }
}
