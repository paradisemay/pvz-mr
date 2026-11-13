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

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private XRGrabInteractable _grab;
        private float _beamOffAt;

        /// <summary>Raised when the trigger is pulled and the ray hits a box.</summary>
        public event Action<QrBox> BoxScanned;

        /// <summary>Raised when the trigger is pulled but no box is in front of the scanner.</summary>
        public event Action Missed;

        /// <summary>Raised when the player picks the scanner up.</summary>
        public event Action Grabbed;

        public bool IsHeld => _grab != null && _grab.isSelected;

        public string ScreenText => screen != null ? screen.text : string.Empty;

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

        private void OnSelected(SelectEnterEventArgs args) => Grabbed?.Invoke();

        /// <summary>Same as picking the scanner up with a controller (tests, debug).</summary>
        internal void SimulateGrab() => Grabbed?.Invoke();

        private void Update()
        {
            if (beam != null && beam.enabled && Time.time >= _beamOffAt) beam.enabled = false;
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

        internal void Configure(Transform muzzleTransform, LineRenderer beamRenderer, TMP_Text screenText)
        {
            muzzle = muzzleTransform;
            beam = beamRenderer;
            screen = screenText;
        }
    }
}
