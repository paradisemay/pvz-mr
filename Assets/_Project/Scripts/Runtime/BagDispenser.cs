using System.Collections.Generic;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// Keeps one bag lying on the counter. When it is taken, a new one appears — but only once the spot is free
    /// (never inside a bag the player is still holding), and at most <see cref="maxLooseBags"/> bags exist at a time.
    /// </summary>
    public sealed class BagDispenser : MonoBehaviour
    {
        [SerializeField] private ParcelBag bagPrefab;
        [SerializeField, Min(0.05f)] private float takenDistance = 0.25f;
        [SerializeField, Min(1)] private int maxLooseBags = 3;
        [SerializeField] private Vector3 spotHalfExtents = new Vector3(0.16f, 0.15f, 0.06f);

        private readonly List<ParcelBag> _spawned = new List<ParcelBag>();
        private readonly Collider[] _overlap = new Collider[8];
        private ParcelBag _current;

        public ParcelBag Current => _current;

        private void Start() => Refill();

        private void Update()
        {
            if (IsWaitingOnCounter(_current)) return;
            if (!SpotIsClear()) return;
            Refill();
        }

        /// <summary>Puts a bag on the counter right away (also usable in edit mode for previews).</summary>
        internal void Refill()
        {
            if (bagPrefab == null) return;
            RecycleLooseBags();
            _current = Instantiate(bagPrefab, transform.position, transform.rotation, transform.parent);
            _spawned.Add(_current);
        }

        private bool IsWaitingOnCounter(ParcelBag bag) =>
            bag != null && !bag.IsHandedOver && !bag.IsHeld &&
            Vector3.Distance(bag.transform.position, transform.position) <= takenDistance;

        private bool SpotIsClear()
        {
            var count = Physics.OverlapBoxNonAlloc(transform.position, spotHalfExtents, _overlap, transform.rotation, ~0,
                QueryTriggerInteraction.Ignore);
            return count == 0;
        }

        /// <summary>Removes the oldest bags left lying around so they never pile up.</summary>
        private void RecycleLooseBags()
        {
            _spawned.RemoveAll(b => b == null || b.IsHandedOver);
            while (_spawned.Count >= maxLooseBags)
            {
                var oldest = _spawned.Find(b => !b.IsHeld);
                if (oldest == null) break;
                _spawned.Remove(oldest);
                if (Application.isPlaying) Destroy(oldest.gameObject);
                else DestroyImmediate(oldest.gameObject);
            }
        }

        internal void Configure(ParcelBag prefab) => bagPrefab = prefab;
    }
}
