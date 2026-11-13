using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Pvz.Runtime
{
    /// <summary>A carrier bag the customer may ask for. Put it into the handover zone to give it away.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ParcelBag : MonoBehaviour
    {
        [SerializeField] private XRGrabInteractable grabInteractable;
        [SerializeField] private Rigidbody body;

        /// <summary>True once the bag was given to a customer.</summary>
        public bool IsHandedOver { get; private set; }

        /// <summary>True while the player holds the bag (the dispenser then prepares the next one).</summary>
        public bool IsHeld => grabInteractable != null && grabInteractable.isSelected;

        public void HandTo(Transform carrier)
        {
            IsHandedOver = true;
            if (grabInteractable != null) grabInteractable.enabled = false;
            if (body != null)
            {
                body.isKinematic = true;
                body.detectCollisions = false;
                body.interpolation = RigidbodyInterpolation.None;
            }
            transform.SetParent(carrier, worldPositionStays: false);
            transform.localPosition = new Vector3(0.28f, -0.25f, -0.1f);
            transform.localRotation = Quaternion.identity;
        }

        internal void Configure(XRGrabInteractable grab, Rigidbody rigidbody)
        {
            grabInteractable = grab;
            body = rigidbody;
        }
    }
}
