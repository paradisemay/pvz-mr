using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// T045 (#85): the returns bin by the counter — a crate on the floor with a trigger inside. Returned and refused
    /// boxes are put here; the computer's «Сдать возвраты» hands in what lies in it. It only tells who is inside;
    /// what counts as a return is decided by Core (<see cref="Pvz.Core.ReturnsShelf"/>).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class ReturnsBin : MonoBehaviour
    {
        [Tooltip("Moves the counter rig the bin belongs to: the boxes inside ride along (they lie on the floor, not on the counter top).")]
        [SerializeField] private PlacementMover counterMover;

        private readonly Dictionary<QrBox, int> _inside = new Dictionary<QrBox, int>();
        private readonly List<(QrBox box, Transform parent)> _riding = new List<(QrBox, Transform)>();

        internal void Configure(PlacementMover mover) => counterMover = mover;

        private void OnEnable()
        {
            if (counterMover != null) counterMover.MovingChanged += OnCounterMoving;
        }

        private void OnDisable()
        {
            if (counterMover != null) counterMover.MovingChanged -= OnCounterMoving;
        }

        /// <summary>The counter is carried: the boxes in the bin go with it (held still in the bin), and drop in again after.</summary>
        private void OnCounterMoving(bool moving)
        {
            if (moving)
            {
                foreach (var box in BoxesInside)
                {
                    if (box.IsHeld) continue;
                    _riding.Add((box, box.transform.parent));
                    var body = box.GetComponent<Rigidbody>();
                    if (body != null) body.isKinematic = true;
                    box.transform.SetParent(transform, true);
                }
                return;
            }
            foreach (var (box, parent) in _riding)
            {
                if (box == null) continue;
                box.transform.SetParent(parent, true);
                var body = box.GetComponent<Rigidbody>();
                if (body != null && !box.IsHeld)
                {
                    body.isKinematic = false;
                    body.position = box.transform.position;
                    body.rotation = box.transform.rotation;
                }
            }
            _riding.Clear();
        }

        public event Action<QrBox> BoxEntered;
        public event Action<QrBox> BoxLeft;

        public IEnumerable<QrBox> BoxesInside
        {
            get
            {
                foreach (var box in _inside.Keys) if (box != null) yield return box;
            }
        }

        public bool Contains(QrBox box) => box != null && _inside.ContainsKey(box);

        private void OnTriggerEnter(Collider other)
        {
            var box = other.GetComponentInParent<QrBox>();
            if (box == null) return;
            _inside.TryGetValue(box, out var count);
            _inside[box] = count + 1; // a box of several colliders enters once
            if (count == 0) BoxEntered?.Invoke(box);
        }

        private void OnTriggerExit(Collider other)
        {
            var box = other.GetComponentInParent<QrBox>();
            if (box == null || !_inside.TryGetValue(box, out var count)) return;
            if (count > 1)
            {
                _inside[box] = count - 1;
                return;
            }
            _inside.Remove(box);
            BoxLeft?.Invoke(box);
        }

        /// <summary>A box handed in (destroyed) leaves without a trigger exit.</summary>
        public void Forget(QrBox box)
        {
            if (box != null) _inside.Remove(box);
        }
    }
}
