using NUnit.Framework;
using Pvz.Core;
using Pvz.Runtime;

namespace Pvz.Editor.Tests
{
    /// <summary>Player-facing record texts (presenter / summary formatting).</summary>
    public class RecordTextTests
    {
        [Test]
        public void NewRecord_ShowsThePreviousBest()
        {
            Assert.AreEqual("<b>НОВЫЙ РЕКОРД!</b> Было: 450", ShiftSummaryPanel.RecordLine(new RecordResult(525, 450, true), "Обычная"));
            Assert.AreEqual("<b>НОВЫЙ РЕКОРД!</b>", ShiftSummaryPanel.RecordLine(new RecordResult(100, 0, true), "Обычная"));
        }

        [Test]
        public void NoRecord_ShowsTheRecordToBeat()
        {
            Assert.AreEqual("Рекорд (Сложная): 700", ShiftSummaryPanel.RecordLine(new RecordResult(300, 700, false), "Сложная"));
        }

        [Test]
        public void MenuRecords_ListEveryDifficulty()
        {
            Assert.AreEqual("Рекорды: Лёгкая 450 · Обычная 0",
                ShiftPresenter.RecordsText(new[] { ("Лёгкая", 450), ("Обычная", 0) }));
            Assert.IsNull(ShiftPresenter.RecordsText(null));
        }
    }
}
