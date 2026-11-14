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
        public void TheRightBox_SaysThatAndTheCount()
        {
            CollectionAssert.AreEqual(new[] { "match", "items", "n8", "scanned", "n7" }, ScannerVoice.Phrase(Match(8, 7)));
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
        public void TheScannerPrefab_HasEveryRecordedClip()
        {
            var scanner = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/HandScanner.prefab");
            var voice = scanner.GetComponent<ScannerVoice>();
            Assert.IsNotNull(voice, "the scanner speaks");
            var so = new SerializedObject(voice);
            foreach (var field in new[] { "source", "match", "other", "done", "items", "scanned" })
                Assert.IsNotNull(so.FindProperty(field).objectReferenceValue, field);
            var numbers = so.FindProperty("numbers");
            Assert.AreEqual(21, numbers.arraySize, "«ноль» … «двадцать»");
            for (var i = 0; i < numbers.arraySize; i++) Assert.IsNotNull(numbers.GetArrayElementAtIndex(i).objectReferenceValue, "n" + i);
        }
    }
}
