using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Pvz.Runtime
{
    /// <summary>
    /// Spawns supply boxes just below the room's ceiling at free spots around the player; physics drops them.
    /// A spot must be reachable: in clear line of sight from the player (no wall or furniture plane in between),
    /// away from the counter and off the customers' walkway.
    /// Ceiling height comes from an AR ceiling plane when the headset found one, otherwise from the settings.
    /// </summary>
    public sealed class SupplyDrop : MonoBehaviour
    {
        private const int Attempts = 60;

        [SerializeField] private QrBox boxPrefab;
        [SerializeField] private Transform player;
        [SerializeField] private Transform counter;
        [SerializeField] private Transform customerSpawn;
        [SerializeField] private ARPlaneManager planes;
        [SerializeField, Min(0.1f)] private float spawnBelowCeiling = 0.35f;
        [SerializeField, Min(0.3f)] private float minDistanceFromPlayer = 0.9f;
        [SerializeField, Min(0.5f)] private float maxDistanceFromPlayer = 2.1f;
        [SerializeField, Min(0.3f)] private float counterClearance = 1.1f;
        [SerializeField, Min(0.2f)] private float walkwayClearance = 0.7f;
        [SerializeField, Min(0.2f)] private float boxSpacing = 0.5f;
        [SerializeField, Min(0.1f)] private float sightHeight = 1.0f;

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

        /// <summary>Spawns one box per id. Deterministic for a given seed.</summary>
        public IReadOnlyList<QrBox> Drop(IReadOnlyList<string> ids, int seed, float fallbackCeilingHeight)
        {
            var random = new System.Random(seed);
            var height = CeilingHeight(fallbackCeilingHeight) - spawnBelowCeiling;
            var placed = new List<Vector3>();
            var spawned = new List<QrBox>();

            foreach (var id in ids)
            {
                var spot = FindSpot(random, placed);
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

        /// <summary>True if a box dropped at this floor spot can be reached by the player.</summary>
        public bool IsGoodSpot(Vector3 spot, IReadOnlyList<Vector3> taken)
        {
            var centre = PlayerFloorPosition;
            var distance = HorizontalDistance(spot, centre);
            if (distance < minDistanceFromPlayer * 0.95f || distance > maxDistanceFromPlayer * 1.05f) return false;
            if (counter != null && HorizontalDistance(spot, counter.position) < counterClearance) return false;
            if (counter != null && customerSpawn != null &&
                DistanceToSegment(spot, counter.position, customerSpawn.position) < walkwayClearance) return false;
            foreach (var other in taken)
            {
                if (HorizontalDistance(spot, other) < boxSpacing) return false;
            }
            // Walls, furniture and AR wall planes between the player and the spot make it unreachable.
            var eye = centre + Vector3.up * sightHeight;
            var target = new Vector3(spot.x, sightHeight, spot.z);
            return !Physics.Linecast(eye, target, ~0, QueryTriggerInteraction.Ignore);
        }

        private Vector3 PlayerFloorPosition
        {
            get
            {
                var p = player != null ? player.position : Vector3.zero;
                p.y = 0f;
                return p;
            }
        }

        private Vector3 FindSpot(System.Random random, List<Vector3> taken)
        {
            var centre = PlayerFloorPosition;
            for (var attempt = 0; attempt < Attempts; attempt++)
            {
                var angle = (float)(random.NextDouble() * Mathf.PI * 2.0);
                var radius = Mathf.Lerp(minDistanceFromPlayer, maxDistanceFromPlayer, (float)random.NextDouble());
                var spot = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (IsGoodSpot(spot, taken)) return spot;
            }

            // Crowded room: drop it right next to the player, on the side away from the counter.
            var away = counter != null ? centre - new Vector3(counter.position.x, 0f, counter.position.z) : Vector3.back;
            if (away.sqrMagnitude < 0.01f) away = Vector3.back;
            return centre + away.normalized * minDistanceFromPlayer + Vector3.right * (0.4f * taken.Count);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            point.y = a.y = b.y = 0f;
            var ab = b - a;
            var t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude);
            return Vector3.Distance(point, a + ab * t);
        }

        internal void Configure(QrBox prefab, Transform playerTransform, Transform counterTransform, Transform spawn, ARPlaneManager planeManager)
        {
            boxPrefab = prefab;
            player = playerTransform;
            counter = counterTransform;
            customerSpawn = spawn;
            planes = planeManager;
        }
    }
}
