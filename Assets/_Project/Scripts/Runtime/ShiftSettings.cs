using System;
using System.Linq;
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

        [Header("Difficulty")]
        [SerializeField] private string displayName = "Обычная";
        [Tooltip("How long a customer waits at the counter, seconds; 0 = forever.")]
        [SerializeField, Min(0)] private float patienceSeconds = 90f;
        [Tooltip("How many customers can stand in line at the counter (1 = one at a time).")]
        [SerializeField, Range(1, 4)] private int queueLength = 2;
        [Tooltip("Length of the shift, seconds; 0 = no time limit (the shift lasts until everything is sold).")]
        [SerializeField, Min(0)] private float shiftDurationSeconds = 420f;
        [Tooltip("Step-by-step hints on the board until the first order is handed over.")]
        [SerializeField] private bool showTutorial;

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
            new BoxDefinition("BOX-101", "3307", Vector3.zero, 0f), // «большой заказ»: three boxes, one order —
            new BoxDefinition("BOX-102", "3307", Vector3.zero, 0f), // «коробок в заказе: 3»
            new BoxDefinition("BOX-104", "3307", Vector3.zero, 0f),
            new BoxDefinition("BOX-103", "2718", Vector3.zero, 0f), // «похожий заказ»: same digits as 2781
        };

        [Header("Spare boxes (T036)")]
        [Tooltip("Other days' orders that fall out of the hatch with the stock: nobody comes for them this shift.")]
        [SerializeField] private BoxDefinition[] spareBoxes =
        {
            new BoxDefinition("BOX-201", "8804", Vector3.zero, 0f),
            new BoxDefinition("BOX-202", "9163", Vector3.zero, 0f),
        };

        [Header("Extra requests")]
        [Tooltip("Chance (0..100) that a customer also asks for a carrier bag.")]
        [SerializeField, Range(0, 100)] private int bagChancePercent = 50;
        [Tooltip("Chance (0..100) that a new customer brings back a box sold earlier in the shift.")]
        [SerializeField, Range(0, 100)] private int returnChancePercent = 20;

        [Header("Random events")]
        [Tooltip("Chance (0..100) that a customer is in a hurry: less patience, bonus points.")]
        [SerializeField, Range(0, 100)] private int urgentChancePercent = 15;
        [Tooltip("Share of the usual patience an urgent customer has.")]
        [SerializeField, Range(0.1f, 1f)] private float urgentPatienceFactor = 0.6f;

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
        [SerializeField] private int intakePoints = 10;
        [SerializeField] private int bagPoints = 20;
        [SerializeField] private int abandonPenalty = 50;
        [SerializeField] private int returnPoints = 30;
        [SerializeField] private int urgentBonus = 50;
        [Tooltip("Share (%) of the shift score that goes to the career: easier shifts count less.")]
        [SerializeField, Range(0, 300)] private int careerPointsPercent = 100;

        [Header("Room")]
        [Tooltip("Used for the supply drop when the headset has not found the ceiling.")]
        [SerializeField, Min(1.5f)] private float defaultCeilingHeight = 2.4f;

        public string DisplayName => displayName;

        /// <summary>Stable id for saved records: the asset name (ShiftSettings_Easy…), not the translatable display name.</summary>
        public string DifficultyId => name;
        public float PatienceSeconds => patienceSeconds;
        public int QueueLength => queueLength;
        public bool ShowTutorial => showTutorial;
        public BoxDefinition[] Boxes => boxes;

        /// <summary>Copies the warehouse layout (boxes and delivery) so every preset matches the scene.</summary>
        internal void CopyLayoutFrom(ShiftSettings other)
        {
            boxes = (BoxDefinition[])other.boxes.Clone();
            supplyBoxes = (BoxDefinition[])other.supplyBoxes.Clone();
            spareBoxes = (BoxDefinition[])other.spareBoxes.Clone();
        }

        /// <summary>Used by the editor builder to create the difficulty presets.</summary>
        internal void ConfigureDifficulty(string name, float patience, int supplyAfter, int bagChance,
            float walkSpeed, float betweenCustomers, int queue, bool tutorial, int returnChance,
            int urgentChance, float shiftSeconds, int careerPercent)
        {
            careerPointsPercent = careerPercent;
            shiftDurationSeconds = shiftSeconds;
            returnChancePercent = returnChance;
            urgentChancePercent = urgentChance;
            queueLength = queue;
            showTutorial = tutorial;
            displayName = name;
            patienceSeconds = patience;
            supplyAfterOrders = supplyAfter;
            bagChancePercent = bagChance;
            customerWalkSpeed = walkSpeed;
            delayBetweenCustomers = betweenCustomers;
        }
        public int SupplyAfterOrders => supplyAfterOrders;
        public BoxDefinition[] SupplyBoxes => supplyBoxes;
        public BoxDefinition[] SpareBoxes => spareBoxes ?? System.Array.Empty<BoxDefinition>();
        public bool HasSupply => supplyAfterOrders > 0 && supplyBoxes != null && supplyBoxes.Length > 0;
        public float DefaultCeilingHeight => defaultCeilingHeight;
        public int BagChancePercent => bagChancePercent;
        public int ReturnChancePercent => returnChancePercent;
        public float ShiftDurationSeconds => shiftDurationSeconds;
        public int CareerPointsPercent => careerPointsPercent;
        public int UrgentChancePercent => urgentChancePercent;
        public float UrgentPatienceFactor => urgentPatienceFactor;

        public SupplyPlan CreateSupplyPlan() => !HasSupply
            ? SupplyPlan.None
            : new SupplyPlan(supplyAfterOrders, System.Linq.Enumerable.Select(supplyBoxes, d => new BoxRecord(new BoxId(d.boxId), d.orderNumber)));
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
            IntakeBox = intakePoints,
            BagService = bagPoints,
            AbandonPenalty = abandonPenalty,
            ReturnService = returnPoints,
            UrgentBonus = urgentBonus,
        };

        public bool TryGetOrderNumber(BoxId id, out string orderNumber)
        {
            foreach (var box in boxes.Concat(SpareBoxes))
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
