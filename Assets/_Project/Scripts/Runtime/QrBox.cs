using Pvz.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Pvz.Runtime
{
    /// <summary>
    /// A parcel box with a QR label. The QR is only a texture; the scanner reads <see cref="Id"/> directly.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class QrBox : MonoBehaviour
    {
        [SerializeField] private string boxId = "BOX-001";
        [SerializeField] private XRGrabInteractable grabInteractable;
        [SerializeField] private Rigidbody body;

        public BoxId Id => new BoxId(boxId);

        public string RawId => boxId;

        /// <summary>True once the box was handed over and belongs to a customer.</summary>
        public bool IsHandedOver { get; private set; }

        private void Reset()
        {
            body = GetComponent<Rigidbody>();
            grabInteractable = GetComponent<XRGrabInteractable>();
        }

        /// <summary>Releases the box from the player's hand and attaches it to the customer.</summary>
        public void HandTo(Transform carrier)
        {
            IsHandedOver = true;
            // Disabling the interactable cancels an active grab.
            if (grabInteractable != null) grabInteractable.enabled = false;
            if (body != null)
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
            transform.SetParent(carrier, worldPositionStays: false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        internal void Configure(string id, XRGrabInteractable grab, Rigidbody rigidbody)
        {
            boxId = id;
            grabInteractable = grab;
            body = rigidbody;
        }
    }
}
