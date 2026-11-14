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
            var hand = ClearShot(target, reach, targetObject, ignore);
            SetPose(hand, Quaternion.LookRotation(target - hand, Vector3.up));
        }

        /// <summary>
        /// A spot <paramref name="reach"/> metres from the target from which a straight line reaches
        /// <paramref name="targetObject"/> first (no other box, counter or wall in between): the player's side first, then
        /// around the target and higher up. Without a target object (or no clear spot) — the player's side.
        /// </summary>
        public static Vector3 ClearShot(Vector3 target, float reach, Transform targetObject, Transform ignore)
        {
            var head = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            var toPlayer = Vector3.ProjectOnPlane(head - target, Vector3.up);
            if (toPlayer.sqrMagnitude < 1e-4f) toPlayer = Vector3.back;
            toPlayer.Normalize();
            var fallback = target + toPlayer * reach + Vector3.up * 0.3f;
            if (targetObject == null) return fallback;
            foreach (var up in new[] { 0.3f, 0.6f, 0.1f, 0.9f })
            foreach (var yaw in new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 135f, -135f, 180f })
            {
                var spot = target + Quaternion.Euler(0f, yaw, 0f) * toPlayer * reach + Vector3.up * up;
                if (FirstHit(spot, target, ignore) is Transform hit && hit.IsChildOf(targetObject)) return spot;
            }
            return fallback;
        }

        private static Transform FirstHit(Vector3 from, Vector3 to, Transform ignore)
        {
            var direction = to - from;
            var hits = Physics.RaycastAll(from, direction.normalized, direction.magnitude + 0.5f, ~0, QueryTriggerInteraction.Ignore);
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

        /// <summary>
        /// Turns the hand over a few frames until <paramref name="muzzle"/> (of something held at an angle, e.g. the
        /// scanner) points at <paramref name="target"/>.
        /// </summary>
        public void AimHeld(Transform muzzle, Vector3 target)
        {
            if (_aim != null) StopCoroutine(_aim);
            _aim = StartCoroutine(AimHeldRoutine(muzzle, target));
        }

        private Coroutine _aim;

        /// <summary>Still turning a held object onto its target (see <see cref="AimHeld"/>).</summary>
        public bool IsAiming => _aim != null;

        private System.Collections.IEnumerator AimHeldRoutine(Transform muzzle, Vector3 target)
        {
            // The held object is attached rigidly (fixed attach point) but follows by velocity tracking, so it lags while
            // the hand moves: wait until both have settled, then solve for the hand rotation that points the muzzle at
            // the target. Two or three passes absorb the muzzle's offset from the hand.
            for (var pass = 0; pass < 4 && muzzle != null; pass++)
            {
                yield return Settle();
                if (muzzle == null) break;
                var toTarget = target - muzzle.position;
                if (Vector3.Angle(muzzle.forward, toTarget) < 0.5f) break;
                var muzzleInHand = Quaternion.Inverse(WorldRotation) * muzzle.rotation;
                TargetRotation = Quaternion.LookRotation(toTarget, Vector3.up) * Quaternion.Inverse(muzzleInHand);
            }
            _aim = null;
        }

        private System.Collections.IEnumerator Settle()
        {
            var deadline = Time.unscaledTime + 5f;
            while (IsMoving && Time.unscaledTime < deadline) yield return null;
            var settleUntil = Time.unscaledTime + 0.4f;
            while (Time.unscaledTime < settleUntil) yield return null;
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
