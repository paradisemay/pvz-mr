using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Pvz.Runtime
{
    /// <summary>
    /// Moving something the player arranges in the room (the counter T034, the supply hatch T036, the rack T037): a handle
    /// appears; the player takes it with grip and carries the thing — it slides at its height and turns around the vertical only. «Применить» keeps the new place (saved),
    /// «Отмена» puts the counter back. Works before and during a shift: the line walks to the new place.
    /// </summary>
    public sealed class PlacementMover : MonoBehaviour
    {
        [SerializeField] private PlacementKeeper keeper;
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
            WakeNeighbours(); // loose boxes the moved thing now touches must feel it, not stay stuck inside it
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
            position = keeper.Clamp(position); // e.g. the hatch stays where its boxes land within reach
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
        [SerializeField] private Vector3 riderZoneCenter;
        [SerializeField] private Vector3 riderZoneSize; // zero: nothing rides along (the hatch)

        private readonly System.Collections.Generic.List<(Rigidbody body, Vector3 position, Quaternion rotation)> _riders =
            new System.Collections.Generic.List<(Rigidbody, Vector3, Quaternion)>();

        private void CollectRiders()
        {
            _riders.Clear();
            if (riderZoneSize.sqrMagnitude < 1e-6f) return;
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
            _riders.RemoveAll(r => r.body == null || IsHeld(r.body)); // taken off meanwhile: it is the player's now, for good
            foreach (var (body, position, rotation) in _riders)
            {
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

        private void WakeNeighbours()
        {
            if (riderZoneSize.sqrMagnitude < 1e-6f || keeper == null || keeper.Rig == null) return;
            var rig = keeper.Rig;
            foreach (var hit in Physics.OverlapBox(rig.TransformPoint(riderZoneCenter), riderZoneSize / 2f + Vector3.one * 0.15f, rig.rotation, ~0,
                         QueryTriggerInteraction.Ignore))
                if (hit.attachedRigidbody != null && !hit.attachedRigidbody.isKinematic) hit.attachedRigidbody.WakeUp();
        }

        private static bool IsHeld(Rigidbody body)
        {
            var grab = body.GetComponent<XRGrabInteractable>();
            return grab != null && grab.isSelected;
        }

        /// <summary>
        /// Puts the moved thing at a pose while moving (e.g. «Стойку ко мне»), carrying what lies on it; nothing is saved —
        /// «Применить» or «Отмена» decide.
        /// </summary>
        public void PlaceWhileMoving(Vector3 position, Quaternion rotation)
        {
            if (!IsMoving) return;
            keeper.Rig.SetPositionAndRotation(keeper.Clamp(new Vector3(position.x, _rigHeight, position.z)), rotation);
            _referenceRigYaw = keeper.Rig.eulerAngles.y;
            _wasHeld = false; // a held handle starts turning from here
            CarryRiders();
            keeper.AfterMoveQuietly();
            if (handle != null && !handle.isSelected) SnapHandle();
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

        internal void Configure(PlacementKeeper placementKeeper, XRGrabInteractable grabHandle, Vector3 offset,
            Vector3 ridersCenter = default, Vector3 ridersSize = default)
        {
            keeper = placementKeeper;
            handle = grabHandle;
            handleOffset = offset;
            riderZoneCenter = ridersCenter;
            riderZoneSize = ridersSize;
        }

        public PlacementKeeper Keeper => keeper;
    }
}
