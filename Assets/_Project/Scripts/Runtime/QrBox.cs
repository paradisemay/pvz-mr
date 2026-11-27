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
        private static Material cardboardMaterial, itmoMaterial;

        private Vector3 _startLocalPosition;
        private Quaternion _startLocalRotation;
        private Transform _startParent;
        private Texture2D _runtimeTexture;

        public BoxId Id => new BoxId(boxId);

        public string RawId => boxId;

        /// <summary>True once the box was handed over and belongs to a customer.</summary>
        public bool IsHandedOver { get; private set; }

        /// <summary>
        /// T045 (#85): a box that came back (a return or a refusal): loose on the counter, to be put into the returns bin.
        /// It is no longer stock, so the handover zone ignores it (<see cref="IsHandedOver"/> stays true).
        /// </summary>
        public bool IsReturn { get; private set; }

        /// <summary>
        /// T045: the box came back to the counter — the player can grab it and carry it to the returns bin.
        /// <paramref name="room"/> is where boxes of the room live (the lost-box check measures reach from it);
        /// <paramref name="home"/> says where a return lost out of reach comes back (the counter, wherever it is then).
        /// </summary>
        public void BecomeReturn(Vector3 position, Quaternion rotation, Transform room, System.Func<Vector3> home)
        {
            // A refused box still in the player's hand is let go first: velocity tracking would yank it back.
            if (grabInteractable != null && grabInteractable.isSelected && grabInteractable.interactionManager != null)
                grabInteractable.interactionManager.CancelInteractableSelection(
                    (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grabInteractable);
            IsHandedOver = true;
            IsReturn = true;
            if (room != null) _startParent = room;
            transform.SetParent(_startParent, true);
            transform.SetPositionAndRotation(position, rotation);
            if (grabInteractable != null) grabInteractable.enabled = true;
            if (body != null)
            {
                body.isKinematic = false;
                body.detectCollisions = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.position = position;
                body.rotation = rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            // A return dropped out of reach comes back to the counter (where it is then), not out of the hatch.
            Respawn = home ?? (() => position);
        }

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

        private void Awake()
        {
            RememberStartPose();
            AddItmoVisual();
        }

        private void AddItmoVisual()
        {
            if (transform.Find("ITMO_Visual") != null) return;
            var model = Resources.Load<GameObject>("ITMO/ITMO_Box");
            if (model == null) return;

            var oldBody = transform.Find("Body");
            if (oldBody != null) oldBody.gameObject.SetActive(false);
            var oldTape = transform.Find("Tape");
            if (oldTape != null) oldTape.gameObject.SetActive(false);

            var topQr = transform.Find("QR_Top");
            if (topQr != null)
            {
                topQr.localPosition = new Vector3(0.115f, 0.151f, 0.095f);
                topQr.localScale = Vector3.one * 0.072f;
            }
            var frontQr = transform.Find("QR_Front");
            if (frontQr != null)
            {
                frontQr.localPosition = new Vector3(0.115f, 0.075f, -0.181f);
                frontQr.localScale = Vector3.one * 0.072f;
            }

            if (cardboardMaterial == null)
                cardboardMaterial = BoxMaterial("ITMO/Cardboard");
            if (itmoMaterial == null)
                itmoMaterial = BoxMaterial("ITMO/Label");

            var visual = Instantiate(model, transform);
            visual.name = "ITMO_Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
                renderer.sharedMaterial = renderer.name.Contains("ITMO") ? itmoMaterial : cardboardMaterial;
        }

        private static Material BoxMaterial(string texturePath)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture(BaseMap, Resources.Load<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white);
            return material;
        }

        private void Update()
        {
            if (IsHandedOver && !IsReturn) return;
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
            ApplyShape();
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

        private void ApplyShape()
        {
            // The last digit keeps each order's parcel shape stable across deliveries and returns.
            var last = boxId[boxId.Length - 1];
            var size = last == '2' || last == '7' ? new Vector3(0.25f, 0.22f, 0.62f) :
                       last == '3' || last == '8' ? new Vector3(0.48f, 0.12f, 0.40f) :
                       last == '4' || last == '9' ? new Vector3(0.58f, 0.60f, 0.50f) :
                       last == '5' || last == '0' ? new Vector3(0.30f, 0.44f, 0.32f) :
                       new Vector3(0.34f, 0.26f, 0.34f);
            var collider = GetComponent<BoxCollider>();
            if (collider != null) collider.size = size;
            var visual = transform.Find("ITMO_Visual");
            if (visual != null) visual.localScale = new Vector3(size.x / 0.34f, size.y / 0.26f, size.z / 0.34f);
            if (body != null) body.mass = 1.2f * size.x * size.y * size.z / (0.34f * 0.26f * 0.34f);

            var label = Mathf.Min(0.072f, size.y * 0.7f);
            PlaceLabel("QR_Top", new Vector3(size.x * 0.34f, size.y / 2f + 0.021f, size.z * 0.28f), label);
            PlaceLabel("QR_Front", new Vector3(size.x * 0.34f, Mathf.Min(size.y * 0.29f, (size.y - label) / 2f), -size.z / 2f - 0.011f), label);
            PlaceLabel("QR_Back", new Vector3(0f, 0f, size.z / 2f + 0.002f), label);
            PlaceLabel("QR_Left", new Vector3(-size.x / 2f - 0.002f, 0f, 0f), label);
            PlaceLabel("QR_Right", new Vector3(size.x / 2f + 0.002f, 0f, 0f), label);
        }

        private void PlaceLabel(string name, Vector3 position, float size)
        {
            var label = transform.Find(name);
            if (label == null) return;
            label.localPosition = position;
            label.localScale = Vector3.one * size;
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
