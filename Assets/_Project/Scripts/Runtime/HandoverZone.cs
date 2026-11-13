using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// Trigger volume on the counter. Reports each box or bag once when it enters (an object can have several
    /// colliders, so entries are reference-counted).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class HandoverZone : MonoBehaviour
    {
        private readonly Dictionary<Component, int> _colliderCounts = new Dictionary<Component, int>();

        public event Action<QrBox> BoxEntered;
        public event Action<ParcelBag> BagEntered;

        /// <summary>Boxes currently inside the zone (snapshot).</summary>
        public IReadOnlyList<QrBox> BoxesInside
        {
            get
            {
                var result = new List<QrBox>();
                foreach (var item in _colliderCounts.Keys)
                {
                    if (item is QrBox box && box != null && !box.IsHandedOver) result.Add(box);
                }
                return result;
            }
        }

        /// <summary>Bags currently inside the zone (snapshot).</summary>
        public IReadOnlyList<ParcelBag> BagsInside
        {
            get
            {
                var result = new List<ParcelBag>();
                foreach (var item in _colliderCounts.Keys)
                {
                    if (item is ParcelBag bag && bag != null && !bag.IsHandedOver) result.Add(bag);
                }
                return result;
            }
        }

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            var item = Identify(other);
            if (item == null) return;

            _colliderCounts.TryGetValue(item, out var count);
            _colliderCounts[item] = count + 1;
            if (count > 0) return;

            if (item is QrBox box) BoxEntered?.Invoke(box);
            else if (item is ParcelBag bag) BagEntered?.Invoke(bag);
        }

        private void OnTriggerExit(Collider other)
        {
            var item = Identify(other, includeHandedOver: true);
            if (item == null || !_colliderCounts.TryGetValue(item, out var count)) return;
            if (count <= 1) _colliderCounts.Remove(item);
            else _colliderCounts[item] = count - 1;
        }

        /// <summary>Forget an item (e.g. handed over and parented to a customer).</summary>
        public void Forget(Component item) => _colliderCounts.Remove(item);

        private static Component Identify(Collider other, bool includeHandedOver = false)
        {
            var box = other.GetComponentInParent<QrBox>();
            if (box != null) return includeHandedOver || !box.IsHandedOver ? box : null;
            var bag = other.GetComponentInParent<ParcelBag>();
            if (bag != null) return includeHandedOver || !bag.IsHandedOver ? bag : null;
            return null;
        }
    }
}
