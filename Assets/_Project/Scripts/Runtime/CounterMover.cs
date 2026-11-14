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
            _referenceRigYaw = rig.eulerAngles.y;
            _referenceHandleYaw = rig.eulerAngles.y; // the handle is snapped to the rig's turn
            _wasHeld = false;
            CollectRiders();
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
            CarryRiders(); // what lay on the counter goes back with it
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
            foreach (var (body, _, _) in _riders) if (body != null) body.WakeUp();
            MovingChanged?.Invoke(false);
        }

        private void LateUpdate()
        {
            if (!IsMoving || handle == null) return;
            if (!handle.isSelected)
            {
                _wasHeld = false;
                SnapHandle();
                return;
            }
            if (!_wasHeld)
            {
                // A new grab: turning counts from here, so the counter does not jump to wherever the hand points.
                _wasHeld = true;
                _referenceHandleYaw = YawOf(handle.transform.forward, handle.transform.up, _referenceHandleYaw);
                _referenceRigYaw = keeper.Rig.eulerAngles.y;
            }
            Follow(handle.transform.position, handle.transform.forward, handle.transform.up);
        }

        private bool _wasHeld;
        private float _referenceHandleYaw;
        private float _referenceRigYaw;

        /// <summary>
        /// The counter follows a held handle: same offset, on the floor, turned only around the vertical — by as much as
        /// the handle turned since it was taken.
        /// </summary>
        public void Follow(Vector3 handlePosition, Vector3 handleForward, Vector3? handleUp = null)
        {
            var rig = keeper.Rig;
            var handleYaw = YawOf(handleForward, handleUp ?? Vector3.up, _referenceHandleYaw);
            var rotation = Quaternion.Euler(0f, _referenceRigYaw + Mathf.DeltaAngle(_referenceHandleYaw, handleYaw), 0f);
            var position = handlePosition - rotation * handleOffset;
            position.y = _rigHeight;
            rig.SetPositionAndRotation(position, rotation);
            CarryRiders();
            keeper.AfterMoveQuietly();
        }

        /// <summary>
        /// Heading of a held thing: where it points on the floor; when it points almost straight up or down, where its top
        /// points instead (a roll of the wrist must not spin the counter); <paramref name="fallback"/> if neither says.
        /// </summary>
        public static float YawOf(Vector3 forward, Vector3 up, float fallback)
        {
            var flat = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flat.sqrMagnitude < 0.04f) flat = Vector3.ProjectOnPlane(forward.y > 0f ? -up : up, Vector3.up);
            if (flat.sqrMagnitude < 1e-4f) return fallback;
            return Quaternion.LookRotation(flat.normalized, Vector3.up).eulerAngles.y;
        }

        // ---------- what lies on the counter rides along ----------

        [Tooltip("The counter top (rig space): boxes and bags lying there when the move starts ride along with the counter.")]
        [SerializeField] private Vector3 riderZoneCenter = new Vector3(0f, 1.3f, 1f);
        [SerializeField] private Vector3 riderZoneSize = new Vector3(1.5f, 0.5f, 0.65f);

        private readonly System.Collections.Generic.List<(Rigidbody body, Vector3 position, Quaternion rotation)> _riders =
            new System.Collections.Generic.List<(Rigidbody, Vector3, Quaternion)>();

        private void CollectRiders()
        {
            _riders.Clear();
            var rig = keeper.Rig;
            var hits = Physics.OverlapBox(rig.TransformPoint(riderZoneCenter), riderZoneSize / 2f, rig.rotation, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                var body = hit.attachedRigidbody;
                if (body == null || body.transform.IsChildOf(rig)) continue; // the rig carries its own parts
                if (body.GetComponent<QrBox>() == null && body.GetComponent<ParcelBag>() == null) continue;
                if (_riders.Exists(r => r.body == body)) continue;
                _riders.Add((body, rig.InverseTransformPoint(body.position), Quaternion.Inverse(rig.rotation) * body.rotation));
            }
        }

        private void CarryRiders()
        {
            var rig = keeper.Rig;
            foreach (var (body, position, rotation) in _riders)
            {
                if (body == null) continue;
                var grab = body.GetComponent<XRGrabInteractable>();
                if (grab != null && grab.isSelected) continue; // taken off the counter meanwhile
                body.transform.SetPositionAndRotation(rig.TransformPoint(position), rig.rotation * rotation);
                body.position = body.transform.position;
                body.rotation = body.transform.rotation;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
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
