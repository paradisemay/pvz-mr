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

        /// <summary>One buffer and one clip per customer, refilled for every line (no allocations while playing).</summary>
        private float[] _buffer;
        private AudioClip _clip;

        /// <summary>How many lines this customer has actually voiced (tests: audio may be off in batch mode).</summary>
        public int LinesSpoken { get; private set; }

        public float PitchHz => pitchHz;

        public void SetVoice(int variant) =>
            pitchHz = VoicePitches[((variant % VoicePitches.Length) + VoicePitches.Length) % VoicePitches.Length];

        public void Speak(string line)
        {
            // Silent outside play mode (editor screenshots stage scenes without playing them).
            if (string.IsNullOrWhiteSpace(line) || source == null || !Application.isPlaying) return;
            if (_clip == null)
            {
                _buffer = new float[Babble.MaxSamples(Babble.SampleRate)];
                _clip = AudioClip.Create("babble", _buffer.Length, 1, Babble.SampleRate, false);
                source.clip = _clip;
            }
            Babble.Fill(_buffer, line, pitchHz, Babble.SampleRate); // the rest of the buffer is silence
            source.Stop(); // a new line cuts the previous one
            _clip.SetData(_buffer, 0);
            source.Play();
            LinesSpoken++;
        }

        private void OnDestroy()
        {
            if (_clip != null) Destroy(_clip);
        }

        internal void Configure(AudioSource audioSource) => source = audioSource;
    }
}
