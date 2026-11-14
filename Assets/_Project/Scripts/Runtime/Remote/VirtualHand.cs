#if UNITY_EDITOR || PVZ_REMOTE
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using InputCommonUsages = UnityEngine.InputSystem.CommonUsages;

namespace Pvz.Runtime.Remote
{
    /// <summary>
    /// A right-hand controller the remote drives (T030): an <see cref="XRSimulatedController"/> input device, so the
    /// game's real XRI input path (interactors, grab, activate) runs, not the shortcuts the autoplay uses.
    /// Pose is given in world space and converted to tracking space under the XR Origin.
    /// State is written with <c>InputState.Change</c> early in Update, as XRI's own simulator does: a queued event is
    /// consumed by the before-render input update, and the interactors never saw the "pressed this frame" of grip.
    /// </summary>
    [DefaultExecutionOrder(UnityEngine.XR.Interaction.Toolkit.XRInteractionUpdateOrder.k_SimulatedDeviceLifecycleManager)]
    public sealed class VirtualHand : MonoBehaviour
    {
        private XRSimulatedController _device;
        private XRSimulatedControllerState _state;

        public bool IsOn => _device != null;
        public Vector3 WorldPosition { get; private set; }
        public Quaternion WorldRotation { get; private set; } = Quaternion.identity;
        public bool Grip => _state.grip > 0.5f;
        public bool Trigger => _state.trigger > 0.5f;

        /// <summary>What the input device itself reports (diagnostics: is the state reaching the Input System?).</summary>
        public string DeviceReport => _device == null
            ? "none"
            : $"added={_device.added} tracked={_device.isTracked.isPressed} usage={string.Join("/", _device.usages)} " +
              $"pos={_device.devicePosition.ReadValue()} grip={_device.gripButton.isPressed} trigger={_device.triggerButton.isPressed}";

        public void TurnOn()
        {
            if (_device != null) return;
            _device = InputSystem.AddDevice<XRSimulatedController>("PvzRemoteRightHand");
            InputSystem.SetDeviceUsage(_device, InputCommonUsages.RightHand);
            _state.Reset();
            _state.isTracked = true;
            _state.trackingState = 3; // position | rotation
            _state.batteryLevel = 1f;
            InputSystem.QueueStateEvent(_device, _state); // tracked from the first update, so XRI switches to controllers
        }

        public void TurnOff()
        {
            if (_aim != null) { StopCoroutine(_aim); _aim = null; }
            if (_device == null) return;
            InputSystem.RemoveDevice(_device);
            _device = null;
        }

        /// <summary>
        /// Where the hand is heading. It moves there at a human pace (<see cref="MoveSpeed"/>), not in one frame: a held
        /// object follows by velocity tracking, and a one-frame jump of a metre threw the scanner across the room.
        /// </summary>
        public Vector3 TargetPosition { get; private set; }
        public Quaternion TargetRotation { get; private set; } = Quaternion.identity;
        public bool IsMoving => (WorldPosition - TargetPosition).sqrMagnitude > 1e-6f || Quaternion.Angle(WorldRotation, TargetRotation) > 0.1f;

        public const float MoveSpeed = 1.2f;   // m/s
        public const float TurnSpeed = 240f;   // deg/s

        public void SetPose(Vector3 worldPosition, Quaternion worldRotation)
        {
            TargetPosition = worldPosition;
            TargetRotation = worldRotation;
        }

        /// <summary>Puts the hand somewhere at once (when turning it on: nothing is held yet).</summary>
        public void Teleport(Vector3 worldPosition, Quaternion worldRotation)
        {
            SetPose(worldPosition, worldRotation);
            WorldPosition = worldPosition;
            WorldRotation = worldRotation;
        }

        /// <summary>
        /// Reaches towards <paramref name="target"/> as a player would: the hand <paramref name="reach"/> metres away on
        /// the player's side and a bit above it, pointing at it — so the ray is not blocked by the counter even when the
        /// headset lies on a table below the counter top.
        /// </summary>
        public void PointAt(Vector3 target, float reach = 0.5f, Transform targetObject = null, Transform ignore = null)
        {
            StopAiming(); // a new target replaces an aim still in progress
            var hand = ClearShot(target, reach, targetObject, ignore);
            SetPose(hand, Quaternion.LookRotation(target - hand, Vector3.up));
        }

        /// <summary>
        /// A spot <paramref name="reach"/> metres from the target from which a straight line reaches
        /// <paramref name="targetObject"/> first (no other box, counter or wall in between): the player's side first, then
        /// around the target and higher up. Without a target object (or no clear spot) — the player's side.
        /// </summary>
        public static Vector3 ClearShot(Vector3 target, float reach, Transform targetObject, Transform ignore) =>
            TryClearShot(target, reach, targetObject, ignore, 0f, out var spot) ? spot : PlayerSide(target, reach);

        private static Vector3 PlayerSide(Vector3 target, float reach)
        {
            var head = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            var toPlayer = Vector3.ProjectOnPlane(head - target, Vector3.up);
            if (toPlayer.sqrMagnitude < 1e-4f) toPlayer = Vector3.back;
            return target + toPlayer.normalized * reach + Vector3.up * 0.3f;
        }

        /// <summary>
        /// Like <see cref="ClearShot"/>, but says when there is no clear spot. A spot is clear when it is not inside
        /// anything and the thin line from it (the scan ray itself) reaches the target first; with a
        /// <paramref name="radius"/>, a line that thick must reach it first too (a held scanner wobbles a little, and a
        /// hair-thin clear line still grazed the next box).
        /// </summary>
        public static bool TryClearShot(Vector3 target, float reach, Transform targetObject, Transform ignore, float radius, out Vector3 spot)
        {
            spot = PlayerSide(target, reach);
            if (targetObject == null) return false;
            var toPlayer = Vector3.ProjectOnPlane(spot - target, Vector3.up).normalized;
            foreach (var up in new[] { 0.3f, 0.6f, 0.1f, 0.9f })
            foreach (var yaw in new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 135f, -135f, 180f })
            {
                var candidate = target + Quaternion.Euler(0f, yaw, 0f) * toPlayer * reach + Vector3.up * up;
                if (Inside(candidate, ignore, targetObject)) continue; // a ray never sees the collider it starts in
                if (!(FirstHit(candidate, target, ignore) is Transform thin && thin.IsChildOf(targetObject))) continue;
                if (radius > 0f && !(FirstHit(candidate, target, ignore, radius) is Transform thick && thick.IsChildOf(targetObject))) continue;
                spot = candidate;
                return true;
            }
            return false;
        }

        private static Transform FirstHit(Vector3 from, Vector3 to, Transform ignore, float radius = 0f)
        {
            var direction = to - from;
            var hits = radius > 0f
                ? Physics.SphereCastAll(from, radius, direction.normalized, direction.magnitude + 0.5f, ~0, QueryTriggerInteraction.Ignore)
                : Physics.RaycastAll(from, direction.normalized, direction.magnitude + 0.5f, ~0, QueryTriggerInteraction.Ignore);
            Transform first = null;
            var best = float.MaxValue;
            foreach (var hit in hits)
            {
                if (ignore != null && hit.collider.transform.IsChildOf(ignore)) continue;
                if (hit.distance >= best) continue;
                best = hit.distance;
                first = hit.collider.transform;
            }
            return first;
        }

        private static readonly Collider[] Overlaps = new Collider[8];

        private static bool Inside(Vector3 point, Transform ignore, Transform targetObject)
        {
            var count = Physics.OverlapSphereNonAlloc(point, 0.02f, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var other = Overlaps[i].transform;
                if ((ignore != null && other.IsChildOf(ignore)) || other.IsChildOf(targetObject)) continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Aims a held object's <paramref name="muzzle"/> (the scanner's ray) at the target from a spot with a clear line
        /// (#93): the clear line is checked from where the muzzle will be, not from the hand — the muzzle sits 10–15 cm
        /// off the hand, and a line clear from the hand still hit the monitor, a bag or the next box in a pile.
        /// The spot is chosen at once (false — and the hand stops — when no spot within reach has a clear line); the hand
        /// then waits until the held object has caught up with it, measures where the muzzle sits in the hand, and goes
        /// where that puts the muzzle on the spot, pointing at the target.
        /// </summary>
        public bool AimHeldAt(Transform muzzle, Vector3 target, Transform targetObject, Transform ignore, float reach = 0.5f)
        {
            StopAiming(); // a new aim, or a refused one, replaces any aim still in progress
            PlannedSpot = null;
            if (muzzle == null) return false;
            var spot = PlayerSide(target, reach); // a bare point (no object to see): from the player's side
            if (targetObject != null && !TryClearShot(target, reach, targetObject, ignore, 0.02f, out spot) &&
                !TryClearShot(target, reach, targetObject, ignore, 0f, out spot)) return false;
            _aim = StartCoroutine(AimHeldRoutine(muzzle, target, spot));
            return true;
        }

        /// <summary>Where the muzzle is being put (diagnostics, tests); set once the held object was measured.</summary>
        public Vector3? PlannedSpot { get; private set; }

        /// <summary>
        /// Turns the hand over a few frames until <paramref name="muzzle"/> (of something held at an angle, e.g. the
        /// scanner) points at <paramref name="target"/>.
        /// </summary>
        public void AimHeld(Transform muzzle, Vector3 target)
        {
            StopAiming();
            _aim = StartCoroutine(AimHeldRoutine(muzzle, target, null));
        }

        private Coroutine _aim;

        private void StopAiming()
        {
            if (_aim == null) return;
            StopCoroutine(_aim);
            _aim = null;
        }

        /// <summary>Still turning a held object onto its target (see <see cref="AimHeld"/>).</summary>
        public bool IsAiming => _aim != null;

        private System.Collections.IEnumerator AimHeldRoutine(Transform muzzle, Vector3 target, Vector3? spot)
        {
            // The held object is attached rigidly (fixed attach point) but follows by velocity tracking, so it lags while
            // the hand moves (and one caught by the far ray is still flying to the hand): wait until both have settled,
            // then solve for the hand pose that puts the muzzle on the spot (or, without one, keeps the hand where it
            // is) pointing at the target. A few passes absorb what is left of the lag.
            var body = muzzle != null ? muzzle.GetComponentInParent<Rigidbody>() : null;
            for (var pass = 0; pass < 4 && muzzle != null; pass++)
            {
                yield return Settle(body);
                if (muzzle == null) break;
                var at = spot ?? muzzle.position;
                var toTarget = target - at;
                if (Vector3.Angle(muzzle.forward, target - muzzle.position) < 0.5f && (muzzle.position - at).sqrMagnitude < 1e-4f) break;
                var muzzleOffset = Quaternion.Inverse(WorldRotation) * (muzzle.position - WorldPosition);
                var muzzleTurn = Quaternion.Inverse(WorldRotation) * muzzle.rotation;
                var hand = Quaternion.LookRotation(toTarget, Vector3.up) * Quaternion.Inverse(muzzleTurn);
                // Turning about the hand would swing the muzzle off the checked spot: move the hand with it.
                SetPose(spot.HasValue ? spot.Value - hand * muzzleOffset : TargetPosition, hand);
                if (spot.HasValue) PlannedSpot = spot;
            }
            _aim = null;
        }

        private System.Collections.IEnumerator Settle(Rigidbody held = null)
        {
            var deadline = Time.unscaledTime + 5f;
            while (IsMoving && Time.unscaledTime < deadline) yield return null;
            var settleUntil = Time.unscaledTime + 0.4f;
            while (Time.unscaledTime < settleUntil) yield return null;
            // A held object still catching up with the hand (velocity tracking, or flying in from the far ray).
            while (held != null && !held.isKinematic && held.linearVelocity.sqrMagnitude > 0.0004f && Time.unscaledTime < deadline + 2f)
                yield return null;
        }

        public void SetGrip(bool on)
        {
            _state.grip = on ? 1f : 0f;
            _state = _state.WithButton(ControllerButton.GripButton, on);
        }

        public void SetTrigger(bool on)
        {
            _state.trigger = on ? 1f : 0f;
            _state = _state.WithButton(ControllerButton.TriggerButton, on);
        }

        private void Update()
        {
            if (_device == null) return;
            WorldPosition = Vector3.MoveTowards(WorldPosition, TargetPosition, MoveSpeed * Time.unscaledDeltaTime);
            WorldRotation = Quaternion.RotateTowards(WorldRotation, TargetRotation, TurnSpeed * Time.unscaledDeltaTime);
            var origin = FindFirstObjectByType<XROrigin>();
            var space = origin != null && origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : null;
            _state.devicePosition = space != null ? space.InverseTransformPoint(WorldPosition) : WorldPosition;
            _state.deviceRotation = space != null ? Quaternion.Inverse(space.rotation) * WorldRotation : WorldRotation;
            UnityEngine.InputSystem.LowLevel.InputState.Change(_device, _state);
        }

        private void OnDestroy() => TurnOff();
    }
}
#endif
