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
        [SerializeField] private float lostBelowY = -1f;
        [SerializeField] private float lostBeyondMeters = 6f;

        private Vector3 _startLocalPosition;
        private Quaternion _startLocalRotation;
        private Transform _startParent;

        public BoxId Id => new BoxId(boxId);

        public string RawId => boxId;

        /// <summary>True once the box was handed over and belongs to a customer.</summary>
        public bool IsHandedOver { get; private set; }

        private void Awake() => RememberStartPose();

        private void Update()
        {
            if (IsHandedOver) return;
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

        private void RememberStartPose()
        {
            _startParent = transform.parent;
            _startLocalPosition = transform.localPosition;
            _startLocalRotation = transform.localRotation;
        }

        internal void Configure(string id, XRGrabInteractable grab, Rigidbody rigidbody)
        {
            boxId = id;
            grabInteractable = grab;
            body = rigidbody;
        }
    }
}
