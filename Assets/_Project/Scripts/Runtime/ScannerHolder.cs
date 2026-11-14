using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Pvz.Runtime
{
    /// <summary>
    /// The scanner's holder on the counter (T038, #76). A scanner let go near the holder clicks into it; a scanner dropped
    /// somewhere else (under the counter, behind real furniture) comes back to the holder after a few seconds at rest,
    /// at once if it fell through the floor or flew out of reach. One left on the counter top stays where it was put.
    /// </summary>
    public sealed class ScannerHolder : MonoBehaviour
    {
        [SerializeField] private HandScanner scanner;
        [Tooltip("Where the scanner lies in the holder.")]
        [SerializeField] private Transform cradle;
        [Tooltip("Moves the counter rig; a scanner lying off the counter is docked when a move starts (it would be dragged along).")]
        [SerializeField] private PlacementMover counterMover;
        [SerializeField, Min(0.02f)] private float snapRadius = 0.18f;
        [Tooltip("Lying still this long off the counter top, the scanner returns to the holder.")]
        [SerializeField, Min(0.5f)] private float returnAfterSeconds = 4f;
        [Tooltip("The counter top, in the counter rig's space: a scanner lying here was put down on purpose and stays.")]
        [SerializeField] private Bounds counterTop = new Bounds(new Vector3(0f, 1.15f, 0f), new Vector3(1.5f, 0.5f, 0.65f));
        [SerializeField, Min(1f)] private float lostDistance = 5f;
        [Tooltip("Half size (holder axes: along, up, across) of the space the scanner takes in the cradle: it does not return into a box or a bag lying there.")]
        [SerializeField] private Vector3 cradleHalfExtents = new Vector3(0.09f, 0.035f, 0.07f);

        private Rigidbody _body;
        private XRGrabInteractable _grab;
        private float _restingSince = float.NaN;
        private readonly Collider[] _overlap = new Collider[8];

        /// <summary>The scanner is in its holder.</summary>
        public bool IsDocked { get; private set; }

        /// <summary>Times the scanner came back on its own (diagnostics, tests).</summary>
        public int Returns { get; private set; }

        /// <summary>The holder's place is taken by something else, so a return waits.</summary>
        public bool CradleBlocked { get; private set; }

        private Transform Rig => transform.parent != null ? transform.parent : transform;

        private void Awake()
        {
            if (scanner == null) return;
            _body = scanner.GetComponent<Rigidbody>();
            _grab = scanner.GetComponent<XRGrabInteractable>();
        }

        private void OnEnable()
        {
            if (_grab != null) _grab.selectExited.AddListener(OnReleased);
            if (counterMover != null) counterMover.MovingChanged += OnCounterMoving;
        }

        private void OnDisable()
        {
            if (_grab != null) _grab.selectExited.RemoveListener(OnReleased);
            if (counterMover != null) counterMover.MovingChanged -= OnCounterMoving;
        }

        private void Start() => Dock(); // the shift starts with the scanner in its holder

        /// <summary>
        /// Let go: the grab restored the docked (kinematic) state — the scanner must fall or click in, not hover.
        /// Runs after XRI's own restore (selectExited follows the drop).
        /// </summary>
        private void OnReleased(SelectExitEventArgs args) => Release();

        internal void Release()
        {
            if (IsHeld) return; // the other hand still holds it
            IsDocked = false;
            _restingSince = float.NaN;
            if (_body != null) _body.isKinematic = false;
        }

        /// <summary>
        /// The scanner stays a child of the counter rig wherever it lies (the grab restores its parent), so a counter move
        /// would drag one lying on the floor through the room: it goes into the holder first.
        /// </summary>
        private void OnCounterMoving(bool moving)
        {
            if (!moving || scanner == null || IsDocked || IsHeld || OnCounterTop(scanner.transform.position)) return;
            if (CradleIsFree()) ReturnHome();
        }

        private bool IsHeld => _grab != null && _grab.isSelected;

        private bool OnCounterTop(Vector3 position) => counterTop.Contains(Rig.InverseTransformPoint(position));

        private void Update()
        {
            if (scanner == null || cradle == null) return;
            if (IsHeld)
            {
                IsDocked = false;
                _restingSince = float.NaN;
                return;
            }
            if (IsDocked) return;

            var position = scanner.transform.position;
            var fromCradle = position - cradle.position;
            if (fromCradle.magnitude <= snapRadius)
            {
                Dock();
                return;
            }
            var lost = position.y < Rig.position.y - 0.5f || new Vector2(fromCradle.x, fromCradle.z).magnitude > lostDistance;
            if (lost)
            {
                if (CradleIsFree()) ReturnHome();
                return;
            }
            var still = _body == null || _body.isKinematic || _body.linearVelocity.sqrMagnitude < 0.0025f;
            var reachedFor = _grab != null && _grab.isHovered; // never snatch it from under a reaching hand
            if (!still || reachedFor || OnCounterTop(position))
            {
                _restingSince = float.NaN;
                return;
            }
            if (float.IsNaN(_restingSince)) _restingSince = Time.time;
            if (Time.time - _restingSince >= returnAfterSeconds && CradleIsFree()) ReturnHome();
        }

        /// <summary>Nothing but the scanner and the holder itself takes the cradle's place (a box or a bag left in the tray).</summary>
        private bool CradleIsFree()
        {
            var count = Physics.OverlapBoxNonAlloc(cradle.position, cradleHalfExtents, _overlap, transform.rotation, ~0,
                QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var other = _overlap[i].transform;
                if (other.IsChildOf(transform) || other.IsChildOf(scanner.transform)) continue;
                CradleBlocked = true;
                return false;
            }
            CradleBlocked = false;
            return true;
        }

        private void ReturnHome()
        {
            Returns++;
            Dock();
        }

        /// <summary>Puts the scanner into the holder (still, kinematic until it is taken).</summary>
        public void Dock()
        {
            if (scanner == null || cradle == null) return;
            if (_body != null)
            {
                if (!_body.isKinematic)
                {
                    _body.linearVelocity = Vector3.zero;
                    _body.angularVelocity = Vector3.zero;
                }
                _body.isKinematic = true;
            }
            scanner.transform.SetPositionAndRotation(cradle.position, cradle.rotation);
            if (_body != null)
            {
                _body.position = cradle.position;
                _body.rotation = cradle.rotation;
            }
            IsDocked = true;
            _restingSince = float.NaN;
        }

        internal void Configure(HandScanner handScanner, Transform holderCradle, PlacementMover mover, Bounds counterTopInRig)
        {
            scanner = handScanner;
            cradle = holderCradle;
            counterMover = mover;
            counterTop = counterTopInRig;
        }
    }
}
