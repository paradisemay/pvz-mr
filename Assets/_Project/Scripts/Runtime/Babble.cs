using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>Procedural babble: one short voiced syllable per few letters, pitch wobbling with the letters.</summary>
    public static class Babble
    {
        public const int SampleRate = 22050;
        public const int MinSyllables = 2;
        public const int MaxSyllables = 8;
        private const float SyllableSeconds = 0.09f;
        private const float GapSeconds = 0.035f;

        private static int SyllableSamples(int sampleRate) => Mathf.CeilToInt(SyllableSeconds * sampleRate);
        private static int GapSamples(int sampleRate) => Mathf.CeilToInt(GapSeconds * sampleRate);

        /// <summary>Length of the longest line (<see cref="MaxSyllables"/>), in samples.</summary>
        public static int MaxSamples(int sampleRate) => MaxSyllables * (SyllableSamples(sampleRate) + GapSamples(sampleRate));

        /// <summary>Longer lines take longer to say, within limits (a long line is still a short mumble).</summary>
        public static int SyllableCount(string line)
        {
            var letters = 0;
            if (line != null)
            {
                foreach (var ch in line) if (char.IsLetterOrDigit(ch)) letters++;
            }
            return Mathf.Clamp(letters / 4, MinSyllables, MaxSyllables);
        }

        /// <summary>The babble of a line as a new array of exactly its length.</summary>
        public static float[] Samples(string line, float pitchHz, int sampleRate)
        {
            var data = new float[SyllableCount(line) * (SyllableSamples(sampleRate) + GapSamples(sampleRate))];
            Fill(data, line, pitchHz, sampleRate);
            return data;
        }

        /// <summary>
        /// Writes the babble of a line into <paramref name="buffer"/> and silence after it; returns its length.
        /// The same line always sounds the same (pitch steps come from the letters, not from chance).
        /// </summary>
        public static int Fill(float[] buffer, string line, float pitchHz, int sampleRate)
        {
            var syllables = SyllableCount(line);
            var syllableSamples = SyllableSamples(sampleRate);
            var gapSamples = GapSamples(sampleRate);
            var length = Mathf.Min(buffer.Length, syllables * (syllableSamples + gapSamples));
            System.Array.Clear(buffer, 0, buffer.Length);
            var text = line ?? string.Empty;
            for (var s = 0; s < syllables; s++)
            {
                var letter = text.Length > 0 ? text[(s * 7) % text.Length] : 'a';
                var step = 0.85f + (letter % 7) * 0.05f;                            // 0.85 … 1.15 of the base pitch
                var frequency = pitchHz * step * (s == syllables - 1 ? 0.9f : 1f); // the voice drops at the end
                var offset = s * (syllableSamples + gapSamples);
                for (var i = 0; i < syllableSamples && offset + i < length; i++)
                {
                    var t = i / (float)sampleRate;
                    var envelope = Mathf.Sin(Mathf.PI * i / syllableSamples); // soft attack and release
                    var voiced = Mathf.Sin(2f * Mathf.PI * frequency * t) + 0.45f * Mathf.Sin(4f * Mathf.PI * frequency * t)
                                 + 0.2f * Mathf.Sin(6f * Mathf.PI * frequency * t);
                    buffer[offset + i] = voiced * envelope * 0.18f;
                }
            }
            return length;
        }
    }
}
