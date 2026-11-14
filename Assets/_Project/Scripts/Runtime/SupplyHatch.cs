using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Pvz.Runtime
{
    /// <summary>
    /// The supply hatch in the ceiling (T036, #80): at the start of every shift — and when a delivery comes — boxes drop out
    /// of it one after another and land in a pile below; physics does the rest (they bounce off each other, the floor and
    /// the furniture). The player chooses where the hatch is (settings of the counter computer → «Место люка»).
    /// The hatch hangs under the ceiling: an AR ceiling plane when the headset found one, otherwise the default height.
    /// </summary>
    public sealed class SupplyHatch : MonoBehaviour
    {
        [SerializeField] private QrBox boxPrefab;
        [SerializeField] private ARPlaneManager planes;
        [Tooltip("Where the dropped boxes live (the boxes root of the play area), so they stay put if the hatch moves.")]
        [SerializeField] private Transform boxesRoot;
        [Tooltip("The hatch panel and its doors: put at the ceiling height.")]
        [SerializeField] private Transform panel;
        [SerializeField] private Transform doorLeft;
        [SerializeField] private Transform doorRight;
        [Tooltip("The cord from the hatch down to its handle (stretched to the ceiling height).")]
        [SerializeField] private Transform cord;
        [SerializeField, Min(0.05f)] private float dropInterval = 0.3f;
        [SerializeField, Min(0f)] private float jitter = 0.12f;
        [SerializeField, Min(0.1f)] private float belowCeiling = 0.3f;
        [SerializeField, Min(0.5f)] private float handleHeight = 1.55f;

        private int _dropping;

        /// <summary>Boxes are still falling out of the hatch.</summary>
        public bool IsDropping => _dropping > 0;

        /// <summary>Raised for every box that drops out (the director puts it into the warehouse).</summary>
        public event Action<QrBox> BoxDropped;

        /// <summary>Height of the detected AR ceiling plane, or <paramref name="fallback"/> if none was found.</summary>
        public float CeilingHeight(float fallback)
        {
            if (planes != null)
            {
                foreach (var plane in planes.trackables)
                {
                    if ((plane.classifications & PlaneClassifications.Ceiling) != 0) return plane.center.y;
                }
            }
            return fallback;
        }

        /// <summary>Hangs the hatch under the ceiling and stretches the cord down to the handle.</summary>
        public void FitToCeiling(float fallbackHeight)
        {
            var ceiling = CeilingHeight(fallbackHeight);
            var height = Mathf.Max(handleHeight + 0.3f, ceiling - transform.position.y);
            if (panel != null) panel.localPosition = new Vector3(0f, height, 0f);
            if (cord != null)
            {
                var length = height - handleHeight;
                cord.localPosition = new Vector3(0f, handleHeight + length / 2f, 0f);
                cord.localScale = new Vector3(cord.localScale.x, length / 2f, cord.localScale.z); // a cylinder is 2 units tall
            }
        }

        /// <summary>Where the boxes come out (world).</summary>
        public Vector3 DropPoint => (panel != null ? panel.position : transform.position) - Vector3.up * belowCeiling;

        /// <summary>
        /// Drops one box per id, one after another (a stream that piles up). Deterministic for a given seed.
        /// </summary>
        public IEnumerator Drop(IReadOnlyList<string> ids, int seed, float fallbackCeilingHeight)
        {
            if (ids == null || ids.Count == 0) yield break;
            _dropping++;
            try
            {
                FitToCeiling(fallbackCeilingHeight);
                SetDoorsOpen(true);
                var random = new System.Random(seed);
                foreach (var id in ids)
                {
                    var offset = new Vector3((float)(random.NextDouble() * 2 - 1) * jitter, 0f, (float)(random.NextDouble() * 2 - 1) * jitter);
                    var rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, (float)(random.NextDouble() * 2 - 1) * 10f);
                    var box = Instantiate(boxPrefab, DropPoint + offset, rotation, boxesRoot != null ? boxesRoot : transform.parent);
                    box.AssignId(id, QrTextures.Create(id));
                    var body = box.GetComponent<Rigidbody>();
                    if (body != null) body.angularVelocity = new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f);
                    BoxDropped?.Invoke(box);
                    yield return new WaitForSeconds(dropInterval);
                }
            }
            finally
            {
                _dropping--;
                if (_dropping == 0) SetDoorsOpen(false);
            }
        }

        private void SetDoorsOpen(bool open)
        {
            if (doorLeft != null) doorLeft.localRotation = Quaternion.Euler(0f, 0f, open ? -80f : 0f);
            if (doorRight != null) doorRight.localRotation = Quaternion.Euler(0f, 0f, open ? 80f : 0f);
        }

        internal void Configure(QrBox prefab, ARPlaneManager planeManager, Transform boxes, Transform hatchPanel, Transform left,
            Transform right, Transform cordTransform, float handleY)
        {
            boxPrefab = prefab;
            planes = planeManager;
            boxesRoot = boxes;
            panel = hatchPanel;
            doorLeft = left;
            doorRight = right;
            cord = cordTransform;
            handleHeight = handleY;
        }
    }
}
