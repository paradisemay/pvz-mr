using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>Keeps one bag lying on the counter: as soon as the current one is picked up or moved away, a new one appears.</summary>
    public sealed class BagDispenser : MonoBehaviour
    {
        [SerializeField] private ParcelBag bagPrefab;
        [SerializeField, Min(0.05f)] private float takenDistance = 0.25f;

        private ParcelBag _current;

        public ParcelBag Current => _current;

        private void Start() => Refill();

        private void Update()
        {
            if (_current == null || _current.IsHandedOver || _current.IsHeld ||
                Vector3.Distance(_current.transform.position, transform.position) > takenDistance)
            {
                Refill();
            }
        }

        /// <summary>Puts a bag on the counter right away (also usable in edit mode for previews).</summary>
        internal void Refill()
        {
            if (bagPrefab == null) return;
            _current = Instantiate(bagPrefab, transform.position, transform.rotation, transform.parent);
        }

        internal void Configure(ParcelBag prefab) => bagPrefab = prefab;
    }
}
