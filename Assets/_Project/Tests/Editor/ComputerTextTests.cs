using NUnit.Framework;
using Pvz.Runtime;

namespace Pvz.Editor.Tests
{
    /// <summary>T033 (#77): the shift screen of the counter computer.</summary>
    public class ComputerTextTests
    {
        [Test]
        public void Order_ShowsNumberItemsHandedOverQueueAndTime()
        {
            var text = PvzComputer.OrderScreen("1435", 3, 1, false, null, 2, 125.2, false);
            StringAssert.Contains("Заказ №1435", text);
            StringAssert.Contains("Товаров: 3 · выдано: 1", text);
            StringAssert.Contains("В очереди: 2", text);
            StringAssert.Contains("Время: 2:06", text);
        }

        [Test]
        public void Bag_Return_AndNobody_AreSaidPlainly()
        {
            StringAssert.Contains("Нужен пакет", PvzComputer.OrderScreen("1435", 1, 1, true, null, 0, null, false));
            StringAssert.Contains("Возврат по заказу №2781", PvzComputer.OrderScreen(null, 0, 0, false, "2781", 0, null, false));
            var idle = PvzComputer.OrderScreen(null, 0, 0, false, null, 0, null, false);
            StringAssert.Contains("Ждём клиента", idle);
            StringAssert.DoesNotContain("Время", idle, "no clock without a limit");
        }

        [Test]
        public void ConfirmingTheEnd_IsShownFirst()
        {
            StringAssert.StartsWith("<color", PvzComputer.OrderScreen(null, 0, 0, false, null, 0, null, true));
        }
    }
}
