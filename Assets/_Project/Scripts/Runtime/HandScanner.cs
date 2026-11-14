using System;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Pvz.Runtime
{
    /// <summary>
    /// Handheld barcode scanner. Pull the trigger while holding it: a ray from the muzzle reads the first
    /// <see cref="QrBox"/> it hits. The scanner only reports what it saw; judging the scan is the director's job.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class HandScanner : MonoBehaviour
    {
        [SerializeField] private Transform muzzle;
        [SerializeField, Min(0.1f)] private float range = 3f;
        [SerializeField] private LayerMask scanMask = ~0;
        [SerializeField] private LineRenderer beam;
        [SerializeField, Min(0)] private float beamDuration = 0.15f;
        [SerializeField] private TMP_Text screen;
        [Tooltip("Screen background that lights up for a moment after a scan.")]
        [SerializeField] private Renderer screenBack;
        [SerializeField, Min(0.05f)] private float flashSeconds = 0.35f;

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private XRGrabInteractable _grab;
        private float _beamOffAt;
        private Color _flashColor;
        private float _flashUntil;
        private bool _flashing;
        private MaterialPropertyBlock _block;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        /// <summary>How many times the screen flashed (tests).</summary>
        public int Flashes { get; private set; }

        /// <summary>Raised when the trigger is pulled and the ray hits a box.</summary>
        public event Action<QrBox> BoxScanned;

        /// <summary>Raised when the trigger is pulled but no box is in front of the scanner.</summary>
        public event Action Missed;

        /// <summary>Raised when the player picks the scanner up.</summary>
        public event Action Grabbed;

        /// <summary>Held by the player (a socket or stand holding it does not count).</summary>
        public bool IsHeld => _grab != null && _grab.isSelected &&
                              !(_grab.firstInteractorSelecting is UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor);

        public string ScreenText => screen != null ? screen.text : string.Empty;

        /// <summary>Where the scan ray starts and points.</summary>
        public Transform Muzzle => muzzle != null ? muzzle : transform;

        private void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            if (beam != null) beam.enabled = false;
        }

        private void OnEnable()
        {
            _grab.activated.AddListener(OnActivated);
            _grab.selectEntered.AddListener(OnSelected);
        }

        private void OnDisable()
        {
            _grab.activated.RemoveListener(OnActivated);
            _grab.selectEntered.RemoveListener(OnSelected);
        }

        private void OnSelected(SelectEnterEventArgs args)
        {
            if (args.interactorObject is UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor) return;
            Grabbed?.Invoke();
        }

        /// <summary>Same as picking the scanner up with a controller (tests, debug).</summary>
        internal void SimulateGrab() => Grabbed?.Invoke();

        private void Update()
        {
            if (beam != null && beam.enabled && Time.time >= _beamOffAt) beam.enabled = false;
            if (_flashing) UpdateFlash();
        }

        private void OnActivated(ActivateEventArgs args) => Fire();

        /// <summary>Casts the scan ray. Called by the trigger; public for debugging and tests.</summary>
        public void Fire()
        {
            var origin = muzzle != null ? muzzle : transform;
            var box = FindBox(origin.position, origin.forward, out var endPoint);
            ShowBeam(origin.position, endPoint);
            if (box != null) BoxScanned?.Invoke(box);
            else Missed?.Invoke();
        }

        /// <summary>Simulates a successful scan of a specific box (debug / automated tests).</summary>
        public void ScanDirect(QrBox box)
        {
            if (box == null) throw new ArgumentNullException(nameof(box));
            BoxScanned?.Invoke(box);
        }

        public void ShowResult(string text, Color color)
        {
            if (screen == null) return;
            screen.text = text;
            screen.color = color;
        }

        /// <summary>Lights the screen in <paramref name="color"/> for a moment (visual confirmation of a scan).</summary>
        public void Flash(Color color)
        {
            if (screenBack == null) return;
            Flashes++;
            _flashColor = color;
            _flashUntil = Time.time + flashSeconds;
            _flashing = true;
            UpdateFlash();
        }

        private void UpdateFlash()
        {
            var left = Mathf.Clamp01((_flashUntil - Time.time) / flashSeconds);
            if (left <= 0f)
            {
                // Done: drop the override, so the screen shows its material (whatever an artist made it) again.
                screenBack.SetPropertyBlock(null);
                _flashing = false;
                return;
            }
            var material = screenBack.sharedMaterial;
            var rest = material != null && material.HasProperty(BaseColor) ? material.GetColor(BaseColor) : Color.black;
            if (_block == null) _block = new MaterialPropertyBlock();
            _block.SetColor(BaseColor, Color.Lerp(rest, _flashColor * 0.55f, left));
            screenBack.SetPropertyBlock(_block);
        }

        private QrBox FindBox(Vector3 origin, Vector3 direction, out Vector3 endPoint)
        {
            endPoint = origin + direction * range;
            var count = Physics.RaycastNonAlloc(origin, direction, _hits, range, scanMask, QueryTriggerInteraction.Ignore);
            var bestDistance = float.MaxValue;
            QrBox best = null;
            for (var i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.collider.transform.IsChildOf(transform)) continue; // ignore the scanner itself
                if (hit.distance >= bestDistance) continue;
                bestDistance = hit.distance;
                endPoint = hit.point;
                best = hit.collider.GetComponentInParent<QrBox>();
            }
            return best;
        }

        private void ShowBeam(Vector3 from, Vector3 to)
        {
            if (beam == null) return;
            beam.useWorldSpace = true;
            beam.positionCount = 2;
            beam.SetPosition(0, from);
            beam.SetPosition(1, to);
            beam.enabled = true;
            _beamOffAt = Time.time + beamDuration;
        }

        internal void ConfigureScreenBack(Renderer back) => screenBack = back;

        internal void Configure(Transform muzzleTransform, LineRenderer beamRenderer, TMP_Text screenText)
        {
            muzzle = muzzleTransform;
            beam = beamRenderer;
            screen = screenText;
        }
    }
}
