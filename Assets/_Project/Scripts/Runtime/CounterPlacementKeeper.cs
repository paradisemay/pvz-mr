using System;
using Pvz.Core;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// Keeps the place of the counter in the room between sessions (T034, #78). The counter rig (counter, computer,
    /// scanner, bag dispenser, the customers' path) is placed in the headset's tracking space — the room; boxes in the
    /// room are not part of it and stay where they are.
    /// </summary>
    public sealed class CounterPlacementKeeper : MonoBehaviour
    {
        [SerializeField] private Transform rig;
        [Tooltip("On a headset the play area is first put in front of the player; the saved place is restored after that.")]
        [SerializeField] private MrSessionSetup session;

        private CounterPlacementStore _store;
        private Vector3 _defaultLocalPosition;
        private Quaternion _defaultLocalRotation = Quaternion.identity;

        /// <summary>Tests set this before the scene loads, so the real saved place is never read or written.</summary>
        internal static ITextStore StoreOverride { get; set; }

        public Transform Rig => rig;
        public bool HasSaved => _store.TryLoad(out _);

        /// <summary>The rig was moved (restored, applied, reset): whoever walks to it must walk again.</summary>
        public event Action Moved;

        private void Awake()
        {
            _store = new CounterPlacementStore(StoreOverride ?? new PlayerPrefsTextStore());
            if (rig == null) return;
            _defaultLocalPosition = rig.localPosition;
            _defaultLocalRotation = rig.localRotation;
        }

        private void OnEnable()
        {
            if (session != null) session.StartAligned += OnStartAligned;
        }

        private void OnDisable()
        {
            if (session != null) session.StartAligned -= OnStartAligned;
        }

        private void Start()
        {
            if (session == null || !MrSessionSetup.HasHeadset) RestoreSaved(); // no alignment to wait for
        }

        private void OnStartAligned() => RestoreSaved();

        /// <summary>The saved place in the room, or null.</summary>
        public CounterPlacement? Saved => _store.TryLoad(out var placement) ? placement : (CounterPlacement?)null;

        /// <summary>The rig's place now (floor position and yaw in the room).</summary>
        public CounterPlacement Current
        {
            get
            {
                var position = rig.position;
                var forward = Vector3.ProjectOnPlane(rig.forward, Vector3.up);
                var yaw = forward.sqrMagnitude < 1e-6f ? 0f : Quaternion.LookRotation(forward, Vector3.up).eulerAngles.y;
                return new CounterPlacement(position.x, position.z, yaw);
            }
        }

        /// <summary>Puts the rig at the saved place (when there is one). Returns whether it moved.</summary>
        public bool RestoreSaved()
        {
            if (rig == null || !_store.TryLoad(out var placement)) return false;
            // A place far from where the counter stands now (in front of the player) means the room changed — another room,
            // a redrawn boundary. Restoring it would put the counter, and the computer that moves it, out of reach.
            var offset = new Vector2(placement.X - rig.position.x, placement.Z - rig.position.z);
            if (offset.magnitude > MaxRestoreDistance) return false;
            Place(placement);
            return true;
        }

        /// <summary>Saved places farther than this from the player at the start are not restored.</summary>
        public const float MaxRestoreDistance = 5f;

        /// <summary>Remembers where the rig stands now.</summary>
        public void SaveCurrent()
        {
            if (rig != null) _store.Save(Current);
        }

        /// <summary>«Стойку ко мне»: the rig back to its place in the play area (in front of the player); that is saved.</summary>
        public void ResetToDefault()
        {
            if (rig == null) return;
            rig.SetLocalPositionAndRotation(_defaultLocalPosition, _defaultLocalRotation);
            AfterMove();
            SaveCurrent();
        }

        /// <summary>Moves the rig to a place in the room (keeps its height).</summary>
        public void Place(CounterPlacement placement)
        {
            if (rig == null) return;
            rig.SetPositionAndRotation(new Vector3(placement.X, rig.position.y, placement.Z), Quaternion.Euler(0f, placement.Yaw, 0f));
            AfterMove();
        }

        /// <summary>Dynamic bodies of the rig (the scanner, a bag) are teleported cleanly with it.</summary>
        internal void AfterMove()
        {
            AfterMoveQuietly();
            Moved?.Invoke();
        }

        /// <summary>As <see cref="AfterMove"/> but without telling anyone (every frame while the handle is carried).</summary>
        internal void AfterMoveQuietly()
        {
            foreach (var body in rig.GetComponentsInChildren<Rigidbody>())
            {
                // Something in the player's hand stays with the hand.
                var grab = body.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                if (grab != null && grab.isSelected) continue;
                body.position = body.transform.position;
                body.rotation = body.transform.rotation;
                if (body.isKinematic) continue;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }
            Physics.SyncTransforms();
        }

        internal void Configure(Transform counterRig, MrSessionSetup mrSession)
        {
            rig = counterRig;
            session = mrSession;
        }
    }
}
