using System;

namespace Pvz.Runtime
{
    /// <summary>
    /// Where a customer holds the next box: a stack in front of the chest that stays below the chin, then stacks at the
    /// sides. Heights are measured up from the carry anchor (chest, 1.0 m; the chin is at ~1.49 m).
    /// </summary>
    public static class CarryStacks
    {
        /// <summary>Highest the chest stack may reach so the face stays visible.</summary>
        public const float FaceClearance = 0.45f;

        /// <summary>Sideways offset of each stack: chest, right, left (boxes are 0.34 m wide, the head 0.26 m).</summary>
        public static readonly float[] ColumnX = { 0f, 0.38f, -0.38f };

        /// <summary>
        /// Index of the stack for a box of <paramref name="height"/>, given the current stack heights. The chest stack
        /// takes the first box whatever its size and any box that still ends below <see cref="FaceClearance"/>;
        /// otherwise the lower of the side stacks (right on a tie).
        /// </summary>
        public static int PickColumn(float[] stacks, float height)
        {
            if (stacks == null || stacks.Length < ColumnX.Length) throw new ArgumentException("one height per stack", nameof(stacks));
            if (stacks[0] <= 0f || stacks[0] + height <= FaceClearance) return 0;
            return stacks[1] <= stacks[2] ? 1 : 2;
        }

        /// <summary>Gap left between stacked boxes.</summary>
        public const float Gap = 0.005f;

        /// <summary>Position of the next box (centre, relative to the carry anchor); grows that stack.</summary>
        public static UnityEngine.Vector3 Place(float[] stacks, float height)
        {
            var column = PickColumn(stacks, height);
            var position = new UnityEngine.Vector3(ColumnX[column], stacks[column] + height / 2f, 0f);
            stacks[column] += height + Gap;
            return position;
        }
    }
}
