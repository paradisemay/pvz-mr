namespace Pvz.Core
{
    /// <summary>
    /// Rules about order numbers as the player reads them: which numbers are easy to mix up (T016). Which orders exist
    /// in the shift is decided elsewhere. (The forgetful customer of T014 is gone: the code is scanned from a phone, T035.)
    /// </summary>
    public static class OrderNumbers
    {
        /// <summary>Shorter numbers are too short to be "almost the same": every pair would look alike.</summary>
        public const int MinLookAlikeLength = 3;

        /// <summary>
        /// Two different numbers that are easy to mix up: the same length (at least <see cref="MinLookAlikeLength"/>)
        /// and either one character different (5812 / 5912) or two neighbours swapped (2781 / 2718).
        /// </summary>
        public static bool LookAlike(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length || a.Length < MinLookAlikeLength || a == b) return false;
            var first = -1;
            var differences = 0;
            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] == b[i]) continue;
                if (differences == 0) first = i;
                differences++;
            }
            if (differences == 1) return true;
            return differences == 2 && first + 1 < a.Length
                   && a[first] == b[first + 1] && a[first + 1] == b[first];
        }

    }
}
