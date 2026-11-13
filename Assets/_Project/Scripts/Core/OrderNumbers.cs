using System;

namespace Pvz.Core
{
    /// <summary>Rules about order numbers as the player reads them.</summary>
    public static class OrderNumbers
    {
        /// <summary>
        /// Two different numbers that are easy to mix up: the same length and either the same digits in another order
        /// (2781 / 2718) or just one digit different (5812 / 5912).
        /// </summary>
        public static bool LookAlike(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length || a == b) return false;
            var differences = 0;
            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) differences++;
            }
            return differences == 1 || SameCharacters(a, b);
        }

        private static bool SameCharacters(string a, string b)
        {
            var left = a.ToCharArray();
            var right = b.ToCharArray();
            Array.Sort(left);
            Array.Sort(right);
            return new string(left) == new string(right);
        }
    }
}
