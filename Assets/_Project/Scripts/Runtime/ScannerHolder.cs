using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Pvz.Runtime
{
    /// <summary>
    /// The scanner's holder on the counter (T038, #76). A scanner let go near the holder clicks into it; a scanner dropped
    /// somewhere else (under the counter, behind real furniture) comes back to the holder after a few seconds at rest,
    /// at once if it fell through the floor or flew out of reach.
    /// </summary>
    public sealed class ScannerHolder : MonoBehaviour
    {
        [SerializeField] private HandScanner scanner;
        [Tooltip("Where the scanner lies in the holder.")]
        [SerializeField] private Transform cradle;
        [SerializeField, Min(0.02f)] private float snapRadius = 0.18f;
        [Tooltip("Lying still this long away from the counter top, the scanner returns to the holder.")]
        [SerializeField, Min(0.5f)] private float returnAfterSeconds = 4f;
        [Tooltip("At the counter-top height and within this distance of the holder the scanner counts as left on the counter on purpose.")]
        [SerializeField, Min(0.1f)] private float counterRadius = 1f;
        [SerializeField, Min(1f)] private float lostDistance = 5f;

        private Rigidbody _body;
        private XRGrabInteractable _grab;
        private float _restingSince = float.NaN;

        /// <summary>The scanner is in its holder.</summary>
        public bool IsDocked { get; private set; }

        /// <summary>Times the scanner came back on its own (diagnostics, tests).</summary>
        public int Returns { get; private set; }

        private void Awake()
        {
            if (scanner == null) return;
            _body = scanner.GetComponent<Rigidbody>();
            _grab = scanner.GetComponent<XRGrabInteractable>();
        }

        private void OnEnable()
        {
            if (_grab != null) _grab.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            if (_grab != null) _grab.selectExited.RemoveListener(OnReleased);
        }

        private void Start() => Dock(); // the shift starts with the scanner in its holder

        /// <summary>Let go: the grab restored the docked (kinematic) state — the scanner must fall or click in, not hover.</summary>
        private void OnReleased(SelectExitEventArgs args) => Release();

        internal void Release()
        {
            IsDocked = false;
            _restingSince = float.NaN;
            if (_body != null) _body.isKinematic = false;
        }

        private void Update()
        {
            if (scanner == null || cradle == null) return;
            if (_grab != null && _grab.isSelected)
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
            var floor = transform.parent != null ? transform.parent.position.y : 0f; // the counter rig stands on the floor
            var lost = position.y < floor - 0.5f || new Vector2(fromCradle.x, fromCradle.z).magnitude > lostDistance;
            if (lost)
            {
                ReturnHome();
                return;
            }
            var still = _body == null || _body.isKinematic || _body.linearVelocity.sqrMagnitude < 0.0025f;
            var onCounter = position.y > transform.position.y - 0.15f && new Vector2(fromCradle.x, fromCradle.z).magnitude <= counterRadius;
            if (!still || onCounter)
            {
                _restingSince = float.NaN;
                return;
            }
            if (float.IsNaN(_restingSince)) _restingSince = Time.time;
            if (Time.time - _restingSince >= returnAfterSeconds) ReturnHome();
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

        internal void Configure(HandScanner handScanner, Transform holderCradle)
        {
            scanner = handScanner;
            cradle = holderCradle;
        }
    }
}
