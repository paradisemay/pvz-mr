using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// Trigger volume on the counter. Reports each box once when it enters (a box has several colliders,
    /// so entries are reference-counted).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class HandoverZone : MonoBehaviour
    {
        private readonly Dictionary<QrBox, int> _colliderCounts = new Dictionary<QrBox, int>();
        private readonly List<QrBox> _scratch = new List<QrBox>();

        public event Action<QrBox> BoxEntered;

        /// <summary>Boxes currently inside the zone (snapshot).</summary>
        public IReadOnlyList<QrBox> BoxesInside
        {
            get
            {
                _scratch.Clear();
                foreach (var pair in _colliderCounts)
                {
                    if (pair.Key != null && !pair.Key.IsHandedOver) _scratch.Add(pair.Key);
                }
                return _scratch.ToArray();
            }
        }

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            var box = other.GetComponentInParent<QrBox>();
            if (box == null || box.IsHandedOver) return;

            _colliderCounts.TryGetValue(box, out var count);
            _colliderCounts[box] = count + 1;
            if (count == 0) BoxEntered?.Invoke(box);
        }

        private void OnTriggerExit(Collider other)
        {
            var box = other.GetComponentInParent<QrBox>();
            if (box == null || !_colliderCounts.TryGetValue(box, out var count)) return;
            if (count <= 1) _colliderCounts.Remove(box);
            else _colliderCounts[box] = count - 1;
        }

        /// <summary>Forget a box (e.g. handed over and parented to a customer).</summary>
        public void Forget(QrBox box) => _colliderCounts.Remove(box);
    }
}
