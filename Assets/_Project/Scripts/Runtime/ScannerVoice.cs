using System.Collections.Generic;
using Pvz.Core;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// T043 (#83): the scanner speaks Russian — «Тот заказ. Семь из восьми» (found so far of the items in the order),
    /// «Не тот заказ», «Заказ выдан». The phrases are clips recorded ahead with a Windows ru-RU voice
    /// (tools/make-voice.ps1), played one after another from the scanner, so the voice comes from the hand that holds it.
    /// A new scan cuts what is still being said (the latest scan matters); «Заказ выдан» waits for it to finish.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class ScannerVoice : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip match;
        [SerializeField] private AudioClip other;
        [SerializeField] private AudioClip done;
        [Tooltip("«из»")]
        [SerializeField] private AudioClip of;
        [Tooltip("«ноль» … «двадцать»: numbers[n] says n.")]
        [SerializeField] private AudioClip[] numbers;
        [Tooltip("«нуля» … «двадцати» (after «из»): ofNumbers[n] says n in the genitive.")]
        [SerializeField] private AudioClip[] ofNumbers;
        [Tooltip("A short pause first, so the scan beep is heard before the voice.")]
        [SerializeField, Min(0f)] private float delay = 0.15f;

        private readonly Queue<AudioClip> _queue = new Queue<AudioClip>();
        private readonly List<string> _spoken = new List<string>();
        private float _startAt;

        /// <summary>The clips the scanner has played, latest last (diagnostics: the remote reports them).</summary>
        public string Spoken => string.Join(" ", _spoken);

        /// <summary>Clips still to be said, the one playing included (diagnostics, tests).</summary>
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
                        phrase.Add("n" + outcome.ItemsScanned);
                        phrase.Add("of");
                        phrase.Add("g" + outcome.ItemsInOrder);
                    }
                    break;
                case ScanStatus.OtherOrder:
                    phrase.Add("other");
                    break;
            }
            return phrase;
        }

        public void SayScan(ScanOutcome outcome)
        {
            var largest = Mathf.Min(numbers != null ? numbers.Length : 0, ofNumbers != null ? ofNumbers.Length : 0) - 1;
            Say(Phrase(outcome, largest), interrupt: true);
        }

        /// <summary>«Заказ выдан» — after the count still being said, not over it.</summary>
        public void SayOrderDone() => Say(new List<string> { "done" }, interrupt: false);

        private void Say(List<string> phrase, bool interrupt)
        {
            if (phrase.Count == 0 || source == null) return;
            var idle = _queue.Count == 0 && !source.isPlaying;
            if (interrupt)
            {
                _queue.Clear();
                source.Stop();
                idle = true;
            }
            foreach (var key in phrase)
            {
                var clip = Clip(key);
                if (clip != null) _queue.Enqueue(clip);
            }
            if (idle) _startAt = Time.unscaledTime + delay;
        }

        private AudioClip Clip(string key)
        {
            switch (key)
            {
                case "match": return match;
                case "other": return other;
                case "done": return done;
                case "of": return of;
            }
            if (key.Length > 1 && int.TryParse(key.Substring(1), out var n))
            {
                var set = key[0] == 'n' ? numbers : key[0] == 'g' ? ofNumbers : null;
                if (set != null && n >= 0 && n < set.Length) return set[n];
            }
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

        /// <summary>The queued clip names (tests).</summary>
        internal List<string> QueuedClips()
        {
            var names = new List<string>();
            foreach (var clip in _queue) names.Add(clip.name);
            return names;
        }

        internal void Configure(AudioSource audioSource, AudioClip matchClip, AudioClip otherClip, AudioClip doneClip,
            AudioClip ofClip, AudioClip[] numberClips, AudioClip[] ofNumberClips)
        {
            source = audioSource;
            match = matchClip;
            other = otherClip;
            done = doneClip;
            of = ofClip;
            numbers = numberClips;
            ofNumbers = ofNumberClips;
        }
    }
}
