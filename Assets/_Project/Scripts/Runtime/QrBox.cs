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

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        private Vector3 _startLocalPosition;
        private Quaternion _startLocalRotation;
        private Transform _startParent;
        private Texture2D _runtimeTexture;

        public BoxId Id => new BoxId(boxId);

        public string RawId => boxId;

        /// <summary>True once the box was handed over and belongs to a customer.</summary>
        public bool IsHandedOver { get; private set; }

        /// <summary>The player holds the box right now.</summary>
        public bool IsHeld => grabInteractable != null && grabInteractable.isSelected;

        /// <summary>Radius of a sphere around the box centre that contains the whole box.</summary>
        public float Reach
        {
            get
            {
                var shape = GetComponentInChildren<BoxCollider>();
                return shape == null ? 0.25f : Vector3.Scale(shape.size, shape.transform.lossyScale).magnitude * 0.5f;
            }
        }

        private void Awake() => RememberStartPose();

        private void Update()
        {
            if (IsHandedOver) return;
            var origin = _startParent != null ? _startParent.position : Vector3.zero;
            var offset = transform.position - origin;
            offset.y = 0f;
            // Fell through the floor, or thrown out of the player's reach — measured from where the box lives (the boxes
            // root of the play area; a box with no parent, e.g. a return box, from the origin). It drops out of the hatch again (T036).
            if (transform.position.y < lostBelowY || !Pvz.Core.WarehouseBounds.IsWithinReach(offset.x, offset.z)) ReturnToStart();
        }

        /// <summary>Puts the box back where the shift started (relative to the play area).</summary>
        /// <summary>Where a lost box comes back (set by the supply hatch); null: where the box first was.</summary>
        public System.Func<Vector3> Respawn { get; set; }

        public void ReturnToStart()
        {
            if (body != null && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            if (Respawn != null) transform.position = Respawn(); // out of the hatch where it is now (T036)
            else transform.localPosition = _startLocalPosition;
            transform.localRotation = _startLocalRotation;
            if (body != null)
            {
                body.position = transform.position;
                body.rotation = transform.rotation;
            }
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
