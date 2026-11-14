using System.Collections.Generic;
using Pvz.Core;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// T043 (#83): the scanner speaks Russian — «Тот заказ. Товаров восемь, отсканировано семь», «Не тот заказ»,
    /// «Заказ выдан». The phrases are clips recorded ahead with a Windows ru-RU voice (tools/make-voice.ps1) and
    /// played one after another from the scanner, so the voice comes from the hand that holds it.
    /// A new phrase cuts the one still playing: the latest scan is what matters.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class ScannerVoice : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip match;
        [SerializeField] private AudioClip other;
        [SerializeField] private AudioClip done;
        [SerializeField] private AudioClip items;
        [SerializeField] private AudioClip scanned;
        [Tooltip("«ноль» … «двадцать»: numbers[n] says n.")]
        [SerializeField] private AudioClip[] numbers;
        [Tooltip("A short pause first, so the scan beep is heard before the voice.")]
        [SerializeField, Min(0f)] private float delay = 0.15f;

        private readonly Queue<AudioClip> _queue = new Queue<AudioClip>();
        private float _startAt;

        /// <summary>The clips the scanner has played, latest last (diagnostics: the remote reports them).</summary>
        public string Spoken => string.Join(" ", _spoken);

        private readonly List<string> _spoken = new List<string>();

        /// <summary>What is still to be said (diagnostics, tests).</summary>
        public int Pending => _queue.Count + (source != null && source.isPlaying ? 1 : 0);

        /// <summary>The clip names of the phrase for a scan: pure, so it is tested without audio.</summary>
        public static List<string> Phrase(ScanOutcome outcome, int largestNumber = 20)
        {
            var phrase = new List<string>();
            switch (outcome.Status)
            {
                case ScanStatus.Matches:
                    phrase.Add("match");
                    if (outcome.ItemsInOrder > 0 && outcome.ItemsInOrder <= largestNumber && outcome.ItemsScanned <= largestNumber)
                    {
                        phrase.Add("items");
                        phrase.Add("n" + outcome.ItemsInOrder);
                        phrase.Add("scanned");
                        phrase.Add("n" + outcome.ItemsScanned);
                    }
                    break;
                case ScanStatus.OtherOrder:
                    phrase.Add("other");
                    break;
            }
            return phrase;
        }

        public void SayScan(ScanOutcome outcome) => Say(Phrase(outcome, numbers != null ? numbers.Length - 1 : -1));

        public void SayOrderDone() => Say(new List<string> { "done" });

        private void Say(List<string> phrase)
        {
            if (phrase.Count == 0 || source == null) return;
            _queue.Clear();
            source.Stop();
            foreach (var key in phrase)
            {
                var clip = Clip(key);
                if (clip != null) _queue.Enqueue(clip);
            }
            _startAt = Time.unscaledTime + delay;
        }

        private AudioClip Clip(string key)
        {
            switch (key)
            {
                case "match": return match;
                case "other": return other;
                case "done": return done;
                case "items": return items;
                case "scanned": return scanned;
            }
            if (key.StartsWith("n") && int.TryParse(key.Substring(1), out var n) && numbers != null && n >= 0 && n < numbers.Length)
                return numbers[n];
            return null;
        }

        private void Update()
        {
            if (_queue.Count == 0 || source == null || source.isPlaying || Time.unscaledTime < _startAt) return;
            source.clip = _queue.Dequeue();
            source.Play();
            _spoken.Add(source.clip.name);
            if (_spoken.Count > 12) _spoken.RemoveAt(0);
        }

        internal void Configure(AudioSource audioSource, AudioClip matchClip, AudioClip otherClip, AudioClip doneClip,
            AudioClip itemsClip, AudioClip scannedClip, AudioClip[] numberClips)
        {
            source = audioSource;
            match = matchClip;
            other = otherClip;
            done = doneClip;
            items = itemsClip;
            scanned = scannedClip;
            numbers = numberClips;
        }
    }
}
