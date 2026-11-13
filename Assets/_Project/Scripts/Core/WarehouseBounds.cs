namespace Pvz.Core
{
    /// <summary>
    /// Where a box may be in the warehouse, in the play area's coordinates (the boxes' root): one rule for saved places
    /// (<see cref="BoxPlacement"/>) and for noticing a lost box (thrown out of reach) in the game. A rule about valid
    /// data, not a tuning knob — so it is here, not in the difficulty settings.
    /// </summary>
    public static class WarehouseBounds
    {
        /// <summary>Farther than this from the play area's centre (horizontally) a box is out of the player's reach.</summary>
        public const float MaxReach = 6f;

        /// <summary>A box centre lower than this would be in the floor (a box is at least ~0.1 m tall).</summary>
        public const float MinHeight = 0.05f;

        /// <summary>Higher than this is above any room's ceiling.</summary>
        public const float MaxHeight = 3f;

        /// <summary>Within reach horizontally (x, z from the play area's centre).</summary>
        public static bool IsWithinReach(float x, float z) => x * x + z * z <= MaxReach * MaxReach;

        /// <summary>A place a box can really stand at: within reach, not in the floor, not above the ceiling.</summary>
        public static bool IsPlausiblePlace(float x, float y, float z) =>
            IsWithinReach(x, z) && y >= MinHeight && y <= MaxHeight;
    }
}
