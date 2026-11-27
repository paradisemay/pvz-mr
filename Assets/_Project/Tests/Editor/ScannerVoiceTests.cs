using System.Linq;
using NUnit.Framework;
using Pvz.Core;
using Pvz.Runtime;
using UnityEditor;
using UnityEngine;

namespace Pvz.Editor.Tests
{
    /// <summary>T043 (#83): the scanner's count of the order on its screen and in its Russian voice.</summary>
    public class ScannerVoiceTests
    {
        private static ScanOutcome Match(int items, int scanned) =>
            new ScanOutcome(ScanStatus.Matches, new BoxId("BOX-101"), "4401", false, items, scanned);

        [Test]
        public void TheRightBox_SaysThatAndTheCount_SevenOfEight()
        {
            CollectionAssert.AreEqual(new[] { "match", "n7", "of", "g8" }, ScannerVoice.Phrase(Match(8, 7)));
        }

        [Test]
        public void AWrongBox_SaysOnlyThat()
        {
            var wrong = new ScanOutcome(ScanStatus.OtherOrder, new BoxId("BOX-201"), "9163", false, 3, 1);
            CollectionAssert.AreEqual(new[] { "other" }, ScannerVoice.Phrase(wrong));
        }

        [Test]
        public void ACountBeyondTheRecordedNumbers_IsNotSaid_TheVerdictIs()
        {
            CollectionAssert.AreEqual(new[] { "match" }, ScannerVoice.Phrase(Match(25, 3)));
        }

        [Test]
        public void OtherScans_AreSilent()
        {
            Assert.IsEmpty(ScannerVoice.Phrase(new ScanOutcome(ScanStatus.CodeNotScanned, new BoxId("BOX-101"), null)));
            Assert.IsEmpty(ScannerVoice.Phrase(new ScanOutcome(ScanStatus.NoActiveOrder, new BoxId("BOX-101"), "4401")));
        }

        private static ScannerVoice VoiceWithSilentClips(GameObject go)
        {
            AudioClip Clip(string name) => AudioClip.Create(name, 2205, 1, 22050, false);
            var voice = go.AddComponent<ScannerVoice>();
            voice.Configure(go.GetComponent<AudioSource>(), Clip("match"), Clip("other"), Clip("done"), Clip("of"),
                Enumerable.Range(0, 21).Select(n => Clip("n" + n)).ToArray(), Enumerable.Range(0, 21).Select(n => Clip("g" + n)).ToArray());
            return voice;
        }

        [Test]
        public void TheOrderDone_WaitsForTheCount_ANewScanCutsIt()
        {
            var go = new GameObject("Voice");
            try
            {
                var voice = VoiceWithSilentClips(go);
                voice.SayScan(Match(8, 7));
                voice.SayOrderDone();
                CollectionAssert.AreEqual(new[] { "match", "n7", "of", "g8", "done" }, voice.QueuedClips(), "«Заказ выдан» after the count");
                voice.SayScan(new ScanOutcome(ScanStatus.OtherOrder, new BoxId("BOX-201"), "9163", false, 3, 1));
                CollectionAssert.AreEqual(new[] { "other" }, voice.QueuedClips(), "the latest scan is what matters");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheScannerScreen_ShowsTheCount()
        {
            var (text, _) = ShiftPresenter.ScanResultText(Match(8, 7));
            StringAssert.Contains("НУЖНАЯ КОРОБКА", text);
            StringAssert.Contains("Товаров: 8", text);
            StringAssert.Contains("Отсканировано: 7", text);
            var (noOrder, _) = ShiftPresenter.ScanResultText(new ScanOutcome(ScanStatus.NoActiveOrder, new BoxId("BOX-101"), "4401"));
            StringAssert.DoesNotContain("Товаров", noOrder, "no customer — no count");
        }

        [Test]
        public void TheScannerPrefab_HasEveryRecordedClip_PreloadedAndWithoutDoppler()
        {
            var scanner = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/HandScanner.prefab");
            var voice = scanner.GetComponent<ScannerVoice>();
            Assert.IsNotNull(voice, "the scanner speaks");
            var so = new SerializedObject(voice);
            foreach (var field in new[] { "source", "match", "other", "done", "of" })
                Assert.IsNotNull(so.FindProperty(field).objectReferenceValue, field);
            foreach (var set in new[] { "numbers", "ofNumbers" })
            {
                var clips = so.FindProperty(set);
                Assert.AreEqual(21, clips.arraySize, set + ": «ноль» … «двадцать»");
                for (var i = 0; i < clips.arraySize; i++)
                {
                    var clip = (AudioClip)clips.GetArrayElementAtIndex(i).objectReferenceValue;
                    Assert.IsNotNull(clip, $"{set}[{i}]");
                    Assert.IsTrue(clip.preloadAudioData, $"{clip.name} is loaded with the scene (no hitch at the first scan)");
                }
            }
            Assert.AreEqual(0f, scanner.GetComponent<AudioSource>().dopplerLevel, "a swung scanner does not wobble the voice");
        }
    }
}
