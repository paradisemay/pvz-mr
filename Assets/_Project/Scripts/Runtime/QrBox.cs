using Pvz.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Pvz.Runtime
{
    /// <summary>
    /// A parcel box with a QR label. The QR is only a texture; the scanner reads <see cref="Id"/> directly.
    /// A box that falls through the floor or is thrown out of reach returns to where it started,
    /// so the shift can always be finished.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class QrBox : MonoBehaviour
    {
        [SerializeField] private string boxId = "BOX-001";
        [SerializeField] private XRGrabInteractable grabInteractable;
        [SerializeField] private Rigidbody body;
        [SerializeField] private Renderer[] labelRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private float lostBelowY = -1f;
        [SerializeField] private float lostBeyondMeters = 6f;

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        private Vector3 _startLocalPosition;
        private Quaternion _startLocalRotation;
        private Transform _startParent;
        private Texture2D _runtimeTexture;

        public BoxId Id => new BoxId(boxId);

        public string RawId => boxId;

        /// <summary>True once the box was handed over and belongs to a customer.</summary>
        public bool IsHandedOver { get; private set; }

        /// <summary>The player has put the box down at least once (see <see cref="PlacedLocalPosition"/>).</summary>
        public bool HasPlacement { get; private set; }

        /// <summary>Where the player last put the box down, relative to where it lives (the boxes root of the play area).</summary>
        public Vector3 PlacedLocalPosition { get; private set; }

        public float PlacedYaw { get; private set; }

        /// <summary><see cref="PlacedLocalPosition"/> in world space (valid even after the box was handed over).</summary>
        public Vector3 PlacedWorldPosition =>
            _startParent != null ? _startParent.TransformPoint(PlacedLocalPosition) : PlacedLocalPosition;

        /// <summary>The player moved the box and has not put it down to rest yet.</summary>
        private bool _movedByPlayer;

        private void Awake() => RememberStartPose();

        private void Update()
        {
            if (IsHandedOver) return;
            TrackPlacement();
            var origin = _startParent != null ? _startParent.position : Vector3.zero;
            var offset = transform.position - origin;
            offset.y = 0f;
            if (transform.position.y < lostBelowY || offset.magnitude > lostBeyondMeters) ReturnToStart();
        }

        /// <summary>Puts the box back where the shift started (relative to the play area).</summary>
        public void ReturnToStart()
        {
            if (body != null && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            transform.localPosition = _startLocalPosition;
            transform.localRotation = _startLocalRotation;
            if (body != null)
            {
                body.position = transform.position;
                body.rotation = transform.rotation;
            }
        }

        /// <summary>A box the player carried is remembered where it comes to rest after being let go.</summary>
        private void TrackPlacement()
        {
            if (grabInteractable != null && grabInteractable.isSelected)
            {
                _movedByPlayer = true;
                return;
            }
            if (_movedByPlayer && body != null && !body.isKinematic && body.IsSleeping()) RecordPlacement();
        }

        /// <summary>Remembers the current pose as the player's place for this box (tests call it directly).</summary>
        internal void RecordPlacement()
        {
            _movedByPlayer = false;
            if (IsHandedOver) return;
            PlacedLocalPosition = _startParent != null ? _startParent.InverseTransformPoint(transform.position) : transform.position;
            PlacedYaw = (_startParent != null ? Quaternion.Inverse(_startParent.rotation) * transform.rotation : transform.rotation).eulerAngles.y;
            HasPlacement = true;
            // A box lost later (fallen through the floor) comes back here, not to where the shift began.
            _startLocalPosition = transform.localPosition;
            _startLocalRotation = transform.localRotation;
        }

        /// <summary>Puts the box where the player left it in an earlier session (upright, at the saved yaw).</summary>
        public void MoveToSavedPlace(Vector3 localPosition, float yaw)
        {
            // Called before the shift, while the box still lives under its start parent (the boxes root).
            transform.localPosition = localPosition;
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.position = transform.position;
                body.rotation = transform.rotation;
            }
            RememberStartPose();
            PlacedLocalPosition = localPosition;
            PlacedYaw = yaw;
            HasPlacement = true;
        }

        /// <summary>Gives a generic (supply) box its identity and QR picture at runtime.</summary>
        public void AssignId(string id, Texture2D qrTexture)
        {
            boxId = new BoxId(id).Value;
            if (qrTexture == null) return;
            ReleaseRuntimeTexture();
            _runtimeTexture = qrTexture;
            var block = new MaterialPropertyBlock();
            foreach (var label in labelRenderers)
            {
                if (label == null) continue;
                label.GetPropertyBlock(block);
                block.SetTexture(BaseMap, qrTexture);
                label.SetPropertyBlock(block);
            }
        }

        /// <summary>Releases the box from the player's hand and attaches it to the customer.</summary>
        public void HandTo(Transform carrier)
        {
            IsHandedOver = true;
            // Disabling the interactable cancels an active grab (XRI restores the body first, we override below).
            if (grabInteractable != null) grabInteractable.enabled = false;
            if (body != null)
            {
                body.isKinematic = true;
                body.detectCollisions = false;
                body.interpolation = RigidbodyInterpolation.None; // follows the customer's transform without lag
            }
            transform.SetParent(carrier, worldPositionStays: false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// A returning customer puts their box on the counter: it stays put (kinematic, not grabbable) but can be
        /// hit by the scanner ray. It is not stock, so the handover zone ignores it (<see cref="IsHandedOver"/>).
        /// </summary>
        public void PresentForScan(Vector3 position, Quaternion rotation)
        {
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(position, rotation);
            if (body != null)
            {
                body.isKinematic = true;
                body.detectCollisions = true;
                body.position = position;
                body.rotation = rotation;
            }
            Physics.SyncTransforms();
        }

        private void OnDestroy() => ReleaseRuntimeTexture();

        /// <summary>The QR texture created for this box at runtime is owned by the box.</summary>
        private void ReleaseRuntimeTexture()
        {
            if (_runtimeTexture == null) return;
            if (Application.isPlaying) Destroy(_runtimeTexture);
            else DestroyImmediate(_runtimeTexture);
            _runtimeTexture = null;
        }

        private void RememberStartPose()
        {
            _startParent = transform.parent;
            _startLocalPosition = transform.localPosition;
            _startLocalRotation = transform.localRotation;
        }

        internal void Configure(string id, XRGrabInteractable grab, Rigidbody rigidbody, Renderer[] labels)
        {
            boxId = id;
            grabInteractable = grab;
            body = rigidbody;
            labelRenderers = labels;
        }
    }
}
