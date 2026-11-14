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

        public void SetPose(Vector3 worldPosition, Quaternion worldRotation)
        {
            WorldPosition = worldPosition;
            WorldRotation = worldRotation;
        }

        /// <summary>
        /// Reaches towards <paramref name="target"/> as a player would: the hand <paramref name="reach"/> metres away on
        /// the player's side and a bit above it, pointing at it — so the ray is not blocked by the counter even when the
        /// headset lies on a table below the counter top.
        /// </summary>
        public void PointAt(Vector3 target, float reach = 0.5f)
        {
            var head = Camera.main != null ? Camera.main.transform.position : transform.position;
            var toPlayer = Vector3.ProjectOnPlane(head - target, Vector3.up);
            if (toPlayer.sqrMagnitude < 1e-4f) toPlayer = Vector3.back;
            var hand = target + toPlayer.normalized * reach + Vector3.up * 0.3f;
            SetPose(hand, Quaternion.LookRotation(target - hand, Vector3.up));
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

        private System.Collections.IEnumerator AimHeldRoutine(Transform muzzle, Vector3 target)
        {
            for (var i = 0; i < 12 && muzzle != null; i++)
            {
                yield return null; // the held object follows the hand one frame later
                var correction = Quaternion.FromToRotation(muzzle.forward, target - muzzle.position);
                WorldRotation = correction * WorldRotation;
            }
            _aim = null;
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
