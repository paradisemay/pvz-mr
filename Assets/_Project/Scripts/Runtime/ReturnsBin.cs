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
        private readonly Dictionary<QrBox, int> _inside = new Dictionary<QrBox, int>();

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
