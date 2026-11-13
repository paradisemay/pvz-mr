using System;
using Pvz.Core;

namespace Pvz.Runtime
{
    /// <summary>
    /// Turns Core shift events into player feedback: board, scanner screen, sounds and customer lines.
    /// All player-facing texts live here. Plain C# object owned by <see cref="ShiftDirector"/>.
    /// </summary>
    public sealed class ShiftPresenter : IDisposable
    {
        private readonly Shift _shift;
        private readonly CounterBoard _board;
        private readonly HandScanner _scanner;
        private readonly FeedbackAudio _audio;

        public ShiftPresenter(Shift shift, CounterBoard board, HandScanner scanner, FeedbackAudio audio)
        {
            _shift = shift ?? throw new ArgumentNullException(nameof(shift));
            // Explicit Unity null checks: serialized references may be "fake null" objects in the editor.
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (scanner == null) throw new ArgumentNullException(nameof(scanner));
            _board = board;
            _scanner = scanner;
            _audio = audio != null ? audio : null;

            _shift.Desk.OrderOpened += OnOrderOpened;
            _shift.Desk.BoxScanned += OnBoxScanned;
            _shift.Desk.BoxHandedOver += OnBoxHandedOver;
            _shift.Desk.OrderCompleted += OnOrderCompleted;
            _shift.Stats.Changed += RefreshScore;
            _shift.DeliveryExpected += OnDeliveryExpected;
            _shift.BoxReceived += OnBoxReceived;
        }

        /// <summary>Customer standing at the counter (lines are spoken through them). Set by the director.</summary>
        public Customer Customer { get; set; }

        public void Dispose()
        {
            _shift.Desk.OrderOpened -= OnOrderOpened;
            _shift.Desk.BoxScanned -= OnBoxScanned;
            _shift.Desk.BoxHandedOver -= OnBoxHandedOver;
            _shift.Desk.OrderCompleted -= OnOrderCompleted;
            _shift.Stats.Changed -= RefreshScore;
            _shift.DeliveryExpected -= OnDeliveryExpected;
            _shift.BoxReceived -= OnBoxReceived;
        }

        public void ShowReceiveSupplyHint()
        {
            _board.ShowHeadline("Примите поставку", CounterBoard.Accent);
            _board.ShowStatus($"Отсканируйте новые коробки: {_shift.PendingIntakeCount} ост.", CounterBoard.Neutral);
        }

        private void OnDeliveryExpected(System.Collections.Generic.IReadOnlyList<BoxRecord> delivery)
        {
            PlaySound(FeedbackAudio.Sound.Beep);
            _board.ShowHeadline("ПОСТАВКА!", CounterBoard.Accent);
            _board.ShowStatus($"Отсканируйте новые коробки: {_shift.DeliveryReceived}/{_shift.DeliverySize}", CounterBoard.Neutral);
            RefreshScore(); // the orders total grew
        }

        private void OnBoxReceived(BoxRecord box)
        {
            PlaySound(FeedbackAudio.Sound.Success);
            _scanner.ShowResult($"Заказ №{box.OrderNumber}\nПРИНЯТО НА СКЛАД", CounterBoard.Good);
            if (!_shift.Desk.HasActiveOrder)
                _board.ShowStatus($"Принято коробок: {_shift.DeliveryReceived}/{_shift.DeliverySize}", CounterBoard.Good);
        }

        public void ShowShiftStarting()
        {
            _board.ShowHeadline("Смена начинается", CounterBoard.Accent);
            _board.ShowStatus("Возьмите сканер со стойки", CounterBoard.Neutral);
            _scanner.ShowResult("Наведите на QR\nи нажмите курок", CounterBoard.Neutral);
            RefreshScore();
        }

        public void ShowCustomerApproaching() => _board.ShowHeadline("Клиент подходит…", CounterBoard.Neutral);

        public void ShowWaitingForCustomer()
        {
            _board.ShowHeadline("Ожидание клиента", CounterBoard.Neutral);
            _board.ShowStatus(string.Empty, CounterBoard.Neutral);
        }

        public void ShowShiftOver()
        {
            _board.ShowHeadline("Смена завершена", CounterBoard.Good);
            _board.ShowStatus("Нажмите «Новая смена», чтобы сыграть ещё", CounterBoard.Neutral);
            PlaySound(FeedbackAudio.Sound.Success);
        }

        public void ShowScanMissed() => _scanner.ShowResult("Нет кода\nв прицеле", CounterBoard.Neutral);

        private void OnOrderOpened(Order order)
        {
            Say($"Здравствуйте!\nЗаказ №{order.Number}");
            _board.ShowHeadline($"Заказ №{order.Number}", CounterBoard.Accent);
            _board.ShowStatus(BoxesLeftText(order.Boxes.Count), CounterBoard.Neutral);
        }

        private void OnBoxScanned(ScanOutcome outcome)
        {
            PlaySound(FeedbackAudio.Sound.Beep);
            switch (outcome.Status)
            {
                case ScanStatus.Matches:
                    _scanner.ShowResult($"Заказ №{outcome.BoxOrderNumber}\nНУЖНАЯ КОРОБКА", CounterBoard.Good);
                    break;
                case ScanStatus.OtherOrder:
                    _scanner.ShowResult($"Заказ №{outcome.BoxOrderNumber}\nНЕ ТОТ ЗАКАЗ", CounterBoard.Bad);
                    break;
                case ScanStatus.NoActiveOrder:
                    _scanner.ShowResult($"Заказ №{outcome.BoxOrderNumber}", CounterBoard.Neutral);
                    break;
                default:
                    _scanner.ShowResult("Код не найден", CounterBoard.Bad);
                    break;
            }
        }

        private void OnBoxHandedOver(HandoverOutcome outcome)
        {
            switch (outcome.Status)
            {
                case HandoverStatus.Accepted:
                    PlaySound(FeedbackAudio.Sound.Success);
                    if (!outcome.OrderCompleted && _shift.Desk.HasActiveOrder)
                        _board.ShowStatus(BoxesLeftText(_shift.Desk.ActiveSession.RemainingCount), CounterBoard.Neutral);
                    break;
                case HandoverStatus.WrongBox:
                    PlaySound(FeedbackAudio.Sound.Error);
                    Say($"Это не мой заказ!\nМне нужен №{_shift.Desk.ActiveOrder.Number}");
                    _board.ShowStatus(outcome.IsRepeat ? "Эту коробку клиент уже вернул" : "Неверная коробка: штраф", CounterBoard.Bad);
                    break;
                case HandoverStatus.NoActiveOrder:
                    _board.ShowStatus("Дождитесь клиента", CounterBoard.Neutral);
                    break;
                case HandoverStatus.UnknownBox:
                    _board.ShowStatus("Этой коробки нет на складе", CounterBoard.Bad);
                    break;
            }
        }

        private void OnOrderCompleted(OrderResult result)
        {
            Say("Спасибо!\nХорошего дня!");
            _board.ShowHeadline("Заказ выдан", CounterBoard.Good);
            // ShiftStats subscribed to the desk before the presenter, so LastOrderPoints is already updated.
            _board.ShowStatus($"+{_shift.Stats.LastOrderPoints} очков", CounterBoard.Good);
        }

        private void RefreshScore() => _board.ShowScore(_shift.Stats.Score, _shift.Stats.OrdersCompleted, _shift.OrdersTotal);

        private void PlaySound(FeedbackAudio.Sound sound)
        {
            if (_audio != null) _audio.Play(sound);
        }

        private void Say(string line)
        {
            if (Customer != null) Customer.Say(line);
        }

        private static string BoxesLeftText(int count) => count == 1 ? "Найдите коробку" : $"Коробок в заказе: {count}";
    }
}
