using NUnit.Framework;
using Pvz.Core;
using Pvz.Runtime;

namespace Pvz.Editor.Tests
{
    /// <summary>T019: the order list on the counter.</summary>
    public class OrderListTextTests
    {
        private static Shift NewShift() =>
            new Shift(new BoxCatalog(new[]
            {
                new BoxRecord(new BoxId("BOX-001"), "1435"),
                new BoxRecord(new BoxId("BOX-002"), "2781"),
                new BoxRecord(new BoxId("BOX-003"), "5812"),
            }), new SystemRandomSource(3)) { QueueLength = 3 };

        [Test]
        public void NobodyYet_NoList() =>
            Assert.AreEqual(string.Empty, ShiftPresenter.OrderListText(NewShift().Book.Entries, 7));

        [Test]
        public void TheCounterComesFirst_ThenTheLine_ThenFinishedOnes()
        {
            using (var shift = NewShift())
            {
                shift.TryEnqueue(out var first);
                shift.TryEnqueue(out var second);
                shift.TryEnqueue(out var third);
                shift.OpenNext(0);
                foreach (var box in first.Order.Boxes) shift.Desk.Handover(box, 1);
                shift.OpenNext(2);

                var lines = ShiftPresenter.OrderListText(shift.Book.Entries, 7).Split('\n');
                Assert.AreEqual("<b>ЗАКАЗЫ</b>", lines[0]);
                StringAssert.Contains($"№{second.Order.Number} — у стойки", lines[1]);
                StringAssert.Contains($"№{third.Order.Number} — ждёт", lines[2]);
                StringAssert.Contains($"№{first.Order.Number} — выдан", lines[3]);
            }
        }

        [Test]
        public void AForgottenNumber_StaysHidden_UntilTheOrderIsHandedOver()
        {
            using (var shift = NewShift())
            {
                shift.ForgotNumberChancePercent = 100;
                shift.TryEnqueue(out var visit);
                Assert.IsTrue(visit.Order.ForgotNumber);
                shift.OpenNext(0);
                var text = ShiftPresenter.OrderListText(shift.Book.Entries, 7);
                StringAssert.Contains("№••" + visit.Order.NumberHint, text);
                StringAssert.DoesNotContain(visit.Order.Number, text);

                foreach (var box in visit.Order.Boxes) shift.Desk.Handover(box, 1);
                StringAssert.Contains($"№{visit.Order.Number} — выдан", ShiftPresenter.OrderListText(shift.Book.Entries, 7));
            }
        }

        [Test]
        public void TheListIsCut_ToTheGivenNumberOfVisits()
        {
            using (var shift = NewShift())
            {
                shift.TryEnqueue(out _);
                shift.TryEnqueue(out _);
                shift.TryEnqueue(out _);
                Assert.AreEqual(1 + 2, ShiftPresenter.OrderListText(shift.Book.Entries, 2).Split('\n').Length);
            }
        }
    }
}
