using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// Customers «speak» their lines: a short procedural babble in their own pitch, from where they stand
    /// (concept: «голосовые реплики клиентов»; a stand-in until real voice lines are recorded).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class CustomerVoice : MonoBehaviour
    {
        /// <summary>Base pitches of the customer looks: low and high voices alternate.</summary>
        private static readonly float[] VoicePitches = { 150f, 240f, 120f, 210f, 180f, 270f };

        [SerializeField] private AudioSource source;
        [SerializeField] private float pitchHz = 180f;

        private AudioClip _clip;

        /// <summary>How many lines this customer has said (tests: audio may be off in batch mode).</summary>
        public int LinesSpoken { get; private set; }

        public float PitchHz => pitchHz;

        public void SetVoice(int variant) =>
            pitchHz = VoicePitches[((variant % VoicePitches.Length) + VoicePitches.Length) % VoicePitches.Length];

        public void Speak(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            LinesSpoken++;
            if (source == null) return;
            if (_clip != null) Destroy(_clip); // one line at a time: the previous babble has long finished
            var samples = Babble.Samples(line, pitchHz, Babble.SampleRate);
            _clip = AudioClip.Create("babble", samples.Length, 1, Babble.SampleRate, false);
            _clip.SetData(samples, 0);
            source.Stop();
            source.PlayOneShot(_clip);
        }

        private void OnDestroy()
        {
            if (_clip != null) Destroy(_clip);
        }

        internal void Configure(AudioSource audioSource) => source = audioSource;
    }

    /// <summary>Procedural babble: one short voiced syllable per few letters, pitch wobbling with the letters.</summary>
    public static class Babble
    {
        public const int SampleRate = 22050;
        public const int MinSyllables = 2;
        public const int MaxSyllables = 8;
        private const float SyllableSeconds = 0.09f;
        private const float GapSeconds = 0.035f;

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

        /// <summary>The same line always sounds the same (pitch steps come from the letters, not from chance).</summary>
        public static float[] Samples(string line, float pitchHz, int sampleRate)
        {
            var syllables = SyllableCount(line);
            var syllableSamples = Mathf.CeilToInt(SyllableSeconds * sampleRate);
            var gapSamples = Mathf.CeilToInt(GapSeconds * sampleRate);
            var data = new float[syllables * (syllableSamples + gapSamples)];
            var text = line ?? string.Empty;
            for (var s = 0; s < syllables; s++)
            {
                var letter = text.Length > 0 ? text[(s * 7) % text.Length] : 'a';
                var step = 0.85f + (letter % 7) * 0.05f;                // 0.85 … 1.15 of the base pitch
                var frequency = pitchHz * step * (s == syllables - 1 ? 0.9f : 1f); // the voice drops at the end
                var offset = s * (syllableSamples + gapSamples);
                for (var i = 0; i < syllableSamples; i++)
                {
                    var t = i / (float)sampleRate;
                    var envelope = Mathf.Sin(Mathf.PI * i / syllableSamples); // soft attack and release
                    var voiced = Mathf.Sin(2f * Mathf.PI * frequency * t) + 0.45f * Mathf.Sin(4f * Mathf.PI * frequency * t)
                                 + 0.2f * Mathf.Sin(6f * Mathf.PI * frequency * t);
                    data[offset + i] = voiced * envelope * 0.18f;
                }
            }
            return data;
        }
    }
}
