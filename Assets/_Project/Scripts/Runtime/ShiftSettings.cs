using System;
using Pvz.Core;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>Tunable parameters of a shift: which boxes exist, pacing and scoring.</summary>
    [CreateAssetMenu(menuName = "PVZ/Shift Settings", fileName = "ShiftSettings")]
    public sealed class ShiftSettings : ScriptableObject
    {
        [Serializable]
        public struct BoxDefinition
        {
            [Tooltip("Text encoded in the QR code, e.g. BOX-001.")]
            public string boxId;
            [Tooltip("Order number the box belongs to, e.g. 1435.")]
            public string orderNumber;
            [Tooltip("Where the scene builder puts the box (centre, metres, player starts at the origin facing +Z).")]
            public Vector3 startPosition;
            [Tooltip("Rotation of the box around the vertical axis, degrees.")]
            public float startYaw;

            public BoxDefinition(string boxId, string orderNumber, Vector3 startPosition, float startYaw)
            {
                this.boxId = boxId;
                this.orderNumber = orderNumber;
                this.startPosition = startPosition;
                this.startYaw = startYaw;
            }
        }

        [Header("Warehouse")]
        [SerializeField] private BoxDefinition[] boxes =
        {
            new BoxDefinition("BOX-001", "1435", new Vector3(-1.35f, 0.13f, 0.35f), 15f),   // floor, left
            new BoxDefinition("BOX-002", "2781", new Vector3(1.45f, 0.87f, -0.55f), -20f),  // table, right
            new BoxDefinition("BOX-003", "5812", new Vector3(-1.55f, 0.61f, -1.45f), 40f),  // bed, behind-left
        };

        [Header("Supply delivery")]
        [Tooltip("The delivery arrives after this many customers were served; 0 = no delivery this shift.")]
        [SerializeField, Min(0)] private int supplyAfterOrders = 1;
        [Tooltip("Boxes of the delivery (start position is ignored: they fall from the ceiling).")]
        [SerializeField] private BoxDefinition[] supplyBoxes =
        {
            new BoxDefinition("BOX-101", "3307", Vector3.zero, 0f),
            new BoxDefinition("BOX-102", "4410", Vector3.zero, 0f),
            new BoxDefinition("BOX-103", "6021", Vector3.zero, 0f),
        };

        [Header("Pacing (seconds, m/s)")]
        [SerializeField, Min(0)] private float firstCustomerDelay = 2f;
        [SerializeField, Min(0)] private float delayBetweenCustomers = 2f;
        [SerializeField, Min(0)] private float thanksDuration = 1.5f;
        [SerializeField, Min(0.1f)] private float customerWalkSpeed = 1.2f;

        [Header("Randomness")]
        [Tooltip("0 = different order sequence every shift.")]
        [SerializeField] private int randomSeed;

        [Header("Scoring")]
        [SerializeField] private int pointsPerOrder = 100;
        [SerializeField] private int flawlessBonus = 50;
        [SerializeField] private float fastThresholdSeconds = 30f;
        [SerializeField] private int fastBonus = 50;
        [SerializeField] private float okThresholdSeconds = 60f;
        [SerializeField] private int okBonus = 25;
        [SerializeField] private int wrongHandoverPenalty = 25;

        public BoxDefinition[] Boxes => boxes;
        public int SupplyAfterOrders => supplyAfterOrders;
        public BoxDefinition[] SupplyBoxes => supplyBoxes;
        public bool HasSupply => supplyAfterOrders > 0 && supplyBoxes != null && supplyBoxes.Length > 0;
        public int RandomSeed => randomSeed;
        public float FirstCustomerDelay => firstCustomerDelay;
        public float DelayBetweenCustomers => delayBetweenCustomers;
        public float ThanksDuration => thanksDuration;
        public float CustomerWalkSpeed => customerWalkSpeed;

        public IRandomSource CreateRandomSource() =>
            randomSeed == 0 ? new SystemRandomSource() : new SystemRandomSource(randomSeed);

        public ScoringRules CreateScoringRules() => new ScoringRules
        {
            OrderCompleted = pointsPerOrder,
            FlawlessBonus = flawlessBonus,
            FastThresholdSeconds = fastThresholdSeconds,
            FastBonus = fastBonus,
            OkThresholdSeconds = okThresholdSeconds,
            OkBonus = okBonus,
            WrongHandoverPenalty = wrongHandoverPenalty,
        };

        public bool TryGetOrderNumber(BoxId id, out string orderNumber)
        {
            foreach (var box in boxes)
            {
                if (BoxId.TryParse(box.boxId, out var candidate) && candidate == id)
                {
                    orderNumber = box.orderNumber;
                    return !string.IsNullOrWhiteSpace(orderNumber);
                }
            }
            orderNumber = null;
            return false;
        }
    }
}
