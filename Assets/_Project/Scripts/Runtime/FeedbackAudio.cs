using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>Short procedural sounds (no audio assets needed): scan beep, success chime, error buzz.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class FeedbackAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;

        private AudioSource _source;
        private AudioClip _beep;
        private AudioClip _success;
        private AudioClip _error;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _beep = Tone("beep", new[] { 1760f }, 0.08f, 0.35f);
            _success = Tone("success", new[] { 880f, 1320f }, 0.12f, 0.4f);
            _error = Tone("error", new[] { 220f, 180f }, 0.16f, 0.45f);
        }

        public enum Sound
        {
            Beep,
            Success,
            Error,
        }

        public void Play(Sound sound)
        {
            switch (sound)
            {
                case Sound.Beep: PlayClip(_beep); break;
                case Sound.Success: PlayClip(_success); break;
                case Sound.Error: PlayClip(_error); break;
            }
        }

        private void PlayClip(AudioClip clip)
        {
            if (_source != null && clip != null) _source.PlayOneShot(clip);
        }

        /// <summary>Sequence of sine notes with a short fade, each <paramref name="noteSeconds"/> long.</summary>
        private static AudioClip Tone(string name, float[] frequencies, float noteSeconds, float volume)
        {
            var samplesPerNote = Mathf.CeilToInt(noteSeconds * SampleRate);
            var data = new float[samplesPerNote * frequencies.Length];
            for (var n = 0; n < frequencies.Length; n++)
            {
                for (var i = 0; i < samplesPerNote; i++)
                {
                    var t = i / (float)SampleRate;
                    var envelope = Mathf.Min(1f, i / 200f) * Mathf.Min(1f, (samplesPerNote - i) / 400f);
                    data[n * samplesPerNote + i] = Mathf.Sin(2f * Mathf.PI * frequencies[n] * t) * envelope * volume;
                }
            }
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
