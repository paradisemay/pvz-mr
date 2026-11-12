using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Pvz.Runtime
{
    /// <summary>
    /// Spawns supply boxes just below the room's ceiling at free spots around the player; physics drops them.
    /// Ceiling height comes from an AR ceiling plane when the headset found one, otherwise from a default.
    /// </summary>
    public sealed class SupplyDrop : MonoBehaviour
    {
        [SerializeField] private QrBox boxPrefab;
        [SerializeField] private Transform player;
        [SerializeField] private Transform counter;
        [SerializeField] private ARPlaneManager planes;
        [SerializeField, Min(1f)] private float defaultCeilingHeight = 2.4f;
        [SerializeField, Min(0.1f)] private float spawnBelowCeiling = 0.35f;
        [SerializeField, Min(0.3f)] private float minDistanceFromPlayer = 0.9f;
        [SerializeField, Min(0.5f)] private float maxDistanceFromPlayer = 2.1f;
        [SerializeField, Min(0.3f)] private float counterClearance = 1.1f;
        [SerializeField, Min(0.2f)] private float boxSpacing = 0.5f;

        public float CeilingHeight
        {
            get
            {
                if (planes != null)
                {
                    foreach (var plane in planes.trackables)
                    {
                        if ((plane.classifications & PlaneClassifications.Ceiling) != 0) return plane.center.y;
                    }
                }
                return defaultCeilingHeight;
            }
        }

        /// <summary>Spawns one box per id. Deterministic for a given seed.</summary>
        public IReadOnlyList<QrBox> Drop(IReadOnlyList<string> ids, int seed)
        {
            var random = new System.Random(seed);
            var height = CeilingHeight - spawnBelowCeiling;
            var placed = new List<Vector3>();
            var spawned = new List<QrBox>();
            var centre = player != null ? player.position : Vector3.zero;
            centre.y = 0f;

            foreach (var id in ids)
            {
                var spot = FindSpot(random, centre, placed);
                placed.Add(spot);
                var rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                var box = Instantiate(boxPrefab, new Vector3(spot.x, height, spot.z), rotation, transform);
                box.AssignId(id, QrTextures.Create(id));
                var body = box.GetComponent<Rigidbody>();
                if (body != null) body.angularVelocity = new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f);
                spawned.Add(box);
            }
            return spawned;
        }

        private Vector3 FindSpot(System.Random random, Vector3 centre, List<Vector3> taken)
        {
            var best = centre + Vector3.left * minDistanceFromPlayer;
            for (var attempt = 0; attempt < 40; attempt++)
            {
                var angle = (float)(random.NextDouble() * Mathf.PI * 2.0);
                var radius = Mathf.Lerp(minDistanceFromPlayer, maxDistanceFromPlayer, (float)random.NextDouble());
                var spot = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (counter != null && HorizontalDistance(spot, counter.position) < counterClearance) continue;
                var free = true;
                foreach (var other in taken)
                {
                    if (HorizontalDistance(spot, other) < boxSpacing) { free = false; break; }
                }
                if (free) return spot;
                best = spot;
            }
            return best;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        internal void Configure(QrBox prefab, Transform playerTransform, Transform counterTransform, ARPlaneManager planeManager)
        {
            boxPrefab = prefab;
            player = playerTransform;
            counter = counterTransform;
            planes = planeManager;
        }
    }
}
