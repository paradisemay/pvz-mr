using System;
using Pvz.Core;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// Keeps the player's arrangement of the warehouse between sessions (concept: «сохранение расположения склада»).
    /// Restores the stock boxes where the player left them, remembers each place as soon as a box is put down, saves.
    /// Independent of the shift: the arrangement is about the room, not about one shift.
    /// </summary>
    public sealed class WarehouseLayoutKeeper : MonoBehaviour
    {
        [Tooltip("The warehouse stock: scene boxes whose places are kept.")]
        [SerializeField] private QrBox[] boxes = Array.Empty<QrBox>();
        [Tooltip("A box put down touching this zone is waiting for a customer, not stored in the warehouse.")]
        [SerializeField] private HandoverZone handoverZone;

        private WarehouseLayoutStore _store;
        private WarehouseLayout _layout;

        /// <summary>
        /// Tests set this before the scene loads, so the keeper never reads or writes the real saved layout of the
        /// developer's machine. Null in the game.
        /// </summary>
        internal static ITextStore StoreOverride { get; set; }

        /// <summary>How many boxes the last <see cref="Restore"/> moved.</summary>
        public int Restored { get; private set; }

        private WarehouseLayout Layout => _layout ?? (_layout = _store.Load());

        /// <summary>Where the scene put each stock box (the layout before the player changed anything).</summary>
        private (Vector3 position, float yaw)[] _defaults = Array.Empty<(Vector3, float)>();

        private void Awake()
        {
            _store = new WarehouseLayoutStore(StoreOverride ?? new PlayerPrefsTextStore());
            _defaults = new (Vector3, float)[boxes.Length];
            for (var i = 0; i < boxes.Length; i++)
            {
                if (boxes[i] == null) continue;
                _defaults[i] = (boxes[i].transform.localPosition, boxes[i].transform.localRotation.eulerAngles.y);
            }
        }

        /// <summary>
        /// «Сбросить склад»: forgets the saved arrangement and puts the stock boxes back where the scene had them
        /// (e.g. the furniture in the room changed). Returns how many boxes were moved back.
        /// </summary>
        public int ResetLayout()
        {
            _layout = WarehouseLayout.Empty;
            Save();
            var moved = 0;
            for (var i = 0; i < boxes.Length; i++)
            {
                var box = boxes[i];
                if (box == null || box.IsHandedOver) continue;
                box.MoveToSavedPlace(_defaults[i].position, _defaults[i].yaw);
                moved++;
            }
            return moved;
        }

        private void OnEnable()
        {
            foreach (var box in boxes) if (box != null) box.Placed += OnPlaced;
        }

        private void OnDisable()
        {
            foreach (var box in boxes) if (box != null) box.Placed -= OnPlaced;
        }

        private void Start() => Restore();

        private void OnApplicationPause(bool paused)
        {
            if (paused) Save(); // the headset was taken off
        }

        /// <summary>Puts the stock boxes at their saved places, where those are still free. Returns how many moved.</summary>
        internal int Restore()
        {
            var moved = 0;
            foreach (var box in boxes)
            {
                if (box == null || box.IsHandedOver || !Layout.TryGet(box.Id, out var place)) continue;
                var spot = new Vector3(place.X, place.Y, place.Z);
                // Something else stands there now (another restored box, the scanner): keep the default place.
                if (!box.IsSpotFree(spot, place.Yaw)) continue;
                box.MoveToSavedPlace(spot, place.Yaw);
                moved++;
            }
            Restored = moved;
            return moved;
        }

        /// <summary>A stock box was put down: that is its place now — unless it waits in the handover zone.</summary>
        private void OnPlaced(QrBox box)
        {
            if (handoverZone != null && handoverZone.Touches(box.PlacedWorldPosition, box.Reach)) return;
            var p = box.PlacedLocalPosition;
            if (Layout.Set(new BoxPlacement(box.Id, p.x, p.y, p.z, box.PlacedYaw))) Save();
        }

        /// <summary>Writes the arrangement; a storage error is logged, never thrown into the game.</summary>
        internal void Save()
        {
            try { _store.Save(Layout); }
            catch (Exception e) { Debug.LogException(e, this); }
        }

        internal void Configure(QrBox[] stock, HandoverZone zone)
        {
            boxes = stock ?? Array.Empty<QrBox>();
            handoverZone = zone;
        }
    }
}
