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
        private readonly FeedbackEffects _effects;

        /// <summary>First-order tutorial: steps only move forward, so re-grabbing the scanner never rewinds a hint.</summary>
        private enum TutorialStep { Off, TakeScanner, Scan, Carry }

        private TutorialStep _step;

        /// <param name="audio">Sounds; null = silent.</param>
        /// <param name="effects">Confetti and other visual confirmation; null = none (e.g. a scene without them).</param>
        public ShiftPresenter(Shift shift, CounterBoard board, HandScanner scanner, FeedbackAudio audio, FeedbackEffects effects,
            bool tutorial = false)
        {
            _shift = shift ?? throw new ArgumentNullException(nameof(shift));
            // Explicit Unity null checks: serialized references may be "fake null" objects in the editor.
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (scanner == null) throw new ArgumentNullException(nameof(scanner));
            _board = board;
            _scanner = scanner;
            _audio = audio != null ? audio : null;
            _effects = effects != null ? effects : null;

            _shift.Desk.OrderOpened += OnOrderOpened;
            _shift.Desk.BoxScanned += OnBoxScanned;
            _shift.Desk.BoxHandedOver += OnBoxHandedOver;
            _shift.Desk.OrderCompleted += OnOrderCompleted;
            _shift.Stats.Changed += RefreshScore;
            _shift.DeliveryExpected += OnDeliveryExpected;
            _shift.BoxReceived += OnBoxReceived;
            _shift.Desk.BagHandedOver += OnBagHandedOver;
            _shift.Desk.BagRequested += OnBagRequested;
            _shift.CustomerHurried += OnCustomerHurried;
            _shift.OrderAbandoned += OnOrderAbandoned;
            _shift.Desk.ReturnOpened += OnReturnOpened;
            _shift.Desk.ReturnAccepted += OnReturnAccepted;
            _shift.TimeUp += OnTimeUp;
            _step = tutorial ? TutorialStep.TakeScanner : TutorialStep.Off;
            _scanner.Grabbed += OnScannerGrabbed;
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
            _shift.Desk.BagHandedOver -= OnBagHandedOver;
            _shift.Desk.BagRequested -= OnBagRequested;
            _shift.CustomerHurried -= OnCustomerHurried;
            _shift.OrderAbandoned -= OnOrderAbandoned;
            _shift.Desk.ReturnOpened -= OnReturnOpened;
            _shift.Desk.ReturnAccepted -= OnReturnAccepted;
            _shift.TimeUp -= OnTimeUp;
            _scanner.Grabbed -= OnScannerGrabbed;
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
            if (!_shift.Desk.HasCustomer)
                _board.ShowStatus($"Принято коробок: {_shift.DeliveryReceived}/{_shift.DeliverySize}", CounterBoard.Good);
        }

        /// <summary>Before the shift: ask the player to pick a difficulty (no shift exists yet).</summary>
        public static void ShowDifficultyMenu(CounterBoard board,
            System.Collections.Generic.IEnumerable<(string name, int best)> records = null)
        {
            board.ShowHeadline("Выберите сложность", CounterBoard.Accent);
            board.ShowStatus(RecordsText(records) ?? "Нажмите кнопку на стойке", CounterBoard.Neutral);
            board.ShowScore(0, 0, 0);
        }

        /// <summary>«Рекорды: Лёгкая 450 · Обычная 0 · Сложная 0», or null when there is nothing to show.</summary>
        public static string RecordsText(System.Collections.Generic.IEnumerable<(string name, int best)> records)
        {
            if (records == null) return null;
            var parts = new System.Collections.Generic.List<string>();
            foreach (var (name, best) in records) parts.Add($"{name} {best}");
            return parts.Count == 0 ? null : "Рекорды: " + string.Join(" · ", parts);
        }

        public void ShowShiftStarting(string difficulty = null)
        {
            _board.ShowHeadline(string.IsNullOrEmpty(difficulty) ? "Смена начинается" : $"Смена: {difficulty}", CounterBoard.Accent);
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

        /// <summary>Patience bar of the customer at the counter (called every frame by the director).</summary>
        public void UpdatePatience(double now)
        {
            if (Customer != null) Customer.ShowPatience(_shift.PatienceRemaining(now));
        }

        private string _clock;

        /// <summary>Shift clock on the board (called every frame; the text changes once a second).</summary>
        public void UpdateClock(double now)
        {
            var clock = ClockText(_shift.TimeLeft(now));
            if (clock == _clock) return;
            _clock = clock;
            RefreshScore();
        }

        /// <summary>«3:12» (seconds rounded up, so «0:00» means the time is really up), or null without a limit.</summary>
        public static string ClockText(double? secondsLeft)
        {
            if (secondsLeft == null) return null;
            var total = (int)Math.Ceiling(secondsLeft.Value);
            return $"{total / 60}:{total % 60:00}";
        }

        private void OnTimeUp(System.Collections.Generic.IReadOnlyList<Visit> sentAway)
        {
            PlaySound(FeedbackAudio.Sound.Error);
            _board.ShowHeadline("Время смены вышло!", CounterBoard.Bad);
            _board.ShowStatus(_shift.Desk.HasCustomer ? "Обслужите последнего клиента" : "Касса закрывается", CounterBoard.Neutral);
        }

        public void ShowScanMissed() => _scanner.ShowResult("Нет кода\nв прицеле", CounterBoard.Neutral);

        private void OnOrderOpened(Order order)
        {
            Say(GreetingFor(order));
            _board.ShowHeadline(order.IsUrgent ? $"СРОЧНО: заказ №{SpokenNumber(order)}" : $"Заказ №{SpokenNumber(order)}",
                order.IsUrgent ? CounterBoard.Bad : CounterBoard.Accent);
            _board.ShowStatus(BoxesLeftText(order.Boxes.Count, order.Boxes.Count), CounterBoard.Neutral);
            if (_step == TutorialStep.TakeScanner && _scanner.IsHeld) _step = TutorialStep.Scan;
            ShowCurrentHint();
        }

        /// <summary>The order number as the customer says it: a forgetful one only knows the end («••81»).</summary>
        internal static string SpokenNumber(Order order) => order.ForgotNumber ? "••" + order.NumberHint : order.Number;

        /// <summary>Opening (depends on urgency) + how the customer names the order (depends on memory).</summary>
        internal static string GreetingFor(Order order)
        {
            var opening = order.IsUrgent ? "Срочно, опаздываю!" : "Здравствуйте!";
            var naming = order.ForgotNumber ? $"Забыл номер… кончается на {order.NumberHint}" : $"Заказ №{order.Number}";
            return opening + "\n" + naming;
        }

        private const string HintTakeScanner = "Подсказка: возьмите сканер со стойки";
        private const string HintScan = "Подсказка: наведите сканер на коробку и нажмите курок";
        private const string HintCarry = "Подсказка: отнесите коробку в зелёную зону";

        private string HintWrongBox => _shift.Desk.HasActiveOrder && _shift.Desk.ActiveOrder.ForgotNumber
            ? $"Подсказка: не та коробка — номер клиента кончается на {_shift.Desk.ActiveOrder.NumberHint}"
            : "Подсказка: не та коробка — ищите № заказа клиента";

        private void ShowCurrentHint()
        {
            switch (_step)
            {
                case TutorialStep.TakeScanner: ShowHint(HintTakeScanner); break;
                case TutorialStep.Scan: ShowHint(HintScan); break;
                case TutorialStep.Carry: ShowHint(HintCarry); break;
            }
        }

        /// <summary>Hint on the status line; for multi-box orders the box count stays visible in front of it.</summary>
        private void ShowHint(string hint)
        {
            var left = _shift.Desk.HasActiveOrder ? _shift.Desk.ActiveSession.RemainingCount : 0;
            _board.ShowStatus(left > 1 ? $"Коробок: {left}. {hint}" : hint, CounterBoard.Accent);
        }

        private void OnScannerGrabbed()
        {
            if (_step != TutorialStep.TakeScanner) return; // only the very first pickup advances the tutorial
            _step = TutorialStep.Scan;
            if (_shift.Desk.HasActiveOrder) ShowCurrentHint();
        }

        private void OnBoxScanned(ScanOutcome outcome)
        {
            PlaySound(FeedbackAudio.Sound.Beep);
            var (text, color) = ScanResultText(outcome);
            _scanner.ShowResult(text, color);
            if (outcome.Status != ScanStatus.NoActiveOrder) _scanner.Flash(color); // every judged scan is confirmed visually
            var tutorialScanning = _step == TutorialStep.Scan || _step == TutorialStep.TakeScanner;
            if (outcome.Status == ScanStatus.Matches && tutorialScanning) { _step = TutorialStep.Carry; ShowCurrentHint(); }
            else if (outcome.Status == ScanStatus.OtherOrder && tutorialScanning) ShowHint(HintWrongBox);
        }

        /// <summary>What the scanner screen shows for a scan (also used for the PR screenshots).</summary>
        public static (string text, UnityEngine.Color color) ScanResultText(ScanOutcome outcome)
        {
            switch (outcome.Status)
            {
                case ScanStatus.Matches:
                    return ($"Заказ №{outcome.BoxOrderNumber}\nНУЖНАЯ КОРОБКА", CounterBoard.Good);
                case ScanStatus.OtherOrder:
                    return (outcome.LooksLikeTheWantedOrder
                        ? $"Заказ №{outcome.BoxOrderNumber}\nПОХОЖ —\nНЕ ТОТ!"
                        : $"Заказ №{outcome.BoxOrderNumber}\nНЕ ТОТ ЗАКАЗ", CounterBoard.Bad);
                case ScanStatus.ReturnAccepted:
                    return ($"Заказ №{outcome.BoxOrderNumber}\nВОЗВРАТ ПРИНЯТ", CounterBoard.Good);
                case ScanStatus.NoActiveOrder:
                    return ($"Заказ №{outcome.BoxOrderNumber}", CounterBoard.Neutral);
                case ScanStatus.Received:
                    return ($"Заказ №{outcome.BoxOrderNumber}\nПРИНЯТО НА СКЛАД", CounterBoard.Good);
                default:
                    return ("Код не найден", CounterBoard.Bad);
            }
        }

        private void OnBoxHandedOver(HandoverOutcome outcome)
        {
            switch (outcome.Status)
            {
                case HandoverStatus.Accepted:
                    PlaySound(FeedbackAudio.Sound.Success);
                    if (outcome.OrderCompleted || !_shift.Desk.HasActiveOrder) break;
                    var left = _shift.Desk.ActiveSession.RemainingCount;
                    if (left > 0) _board.ShowStatus(BoxesLeftText(left, _shift.Desk.ActiveOrder.Boxes.Count), CounterBoard.Neutral);
                    break;
                case HandoverStatus.WrongBox:
                    PlaySound(FeedbackAudio.Sound.Error);
                    Say(outcome.LooksLikeTheWantedOrder
                        ? $"Похож, но не мой!\nМне нужен №{SpokenNumber(_shift.Desk.ActiveOrder)}"
                        : $"Это не мой заказ!\nМне нужен №{SpokenNumber(_shift.Desk.ActiveOrder)}");
                    _board.ShowStatus(outcome.IsRepeat ? "Эту коробку клиент уже вернул" : "Неверная коробка: штраф", CounterBoard.Bad);
                    break;
                case HandoverStatus.NoActiveOrder:
                    _board.ShowStatus(NoOrderText, CounterBoard.Neutral);
                    break;
                case HandoverStatus.UnknownBox:
                    _board.ShowStatus("Этой коробки нет на складе", CounterBoard.Bad);
                    break;
            }
        }

        private void OnBagHandedOver(BagStatus status)
        {
            switch (status)
            {
                case BagStatus.Accepted:
                    PlaySound(FeedbackAudio.Sound.Success);
                    if (_shift.Desk.HasActiveOrder) _board.ShowStatus("Пакет выдан", CounterBoard.Good);
                    break;
                case BagStatus.NotNeeded:
                    Say("Пакет не нужен,\nспасибо");
                    break;
                case BagStatus.TooEarly:
                    Say("Сначала мои\nкоробки, пожалуйста");
                    break;
                case BagStatus.NoActiveOrder:
                    _board.ShowStatus(NoOrderText, CounterBoard.Neutral);
                    break;
            }
        }

        /// <summary>Something was handed over while no order is open: either nobody is there or it is a return.</summary>
        private string NoOrderText => _shift.Desk.HasActiveReturn ? "Сейчас идёт возврат: отсканируйте коробку клиента" : "Дождитесь клиента";

        private void OnBagRequested(Order order)
        {
            Say("Спасибо! Мне ещё\nнужен пакет");
            _board.ShowStatus("Клиенту нужен пакет", CounterBoard.Accent);
        }

        private void OnReturnOpened(ReturnRequest request)
        {
            Say($"Хочу вернуть товар\nиз заказа №{request.OrderNumber}");
            _board.ShowHeadline($"Возврат: заказ №{request.OrderNumber}", CounterBoard.Accent);
            _board.ShowStatus("Отсканируйте коробку клиента на стойке", CounterBoard.Neutral);
        }

        private void OnReturnAccepted(ReturnRequest request)
        {
            PlaySound(FeedbackAudio.Sound.Success);
            Say("Спасибо!\nВсего доброго!");
            Celebrate();
            _board.ShowHeadline("Возврат принят", CounterBoard.Good);
            _board.ShowStatus($"+{_shift.Stats.PointsPerReturn} очков", CounterBoard.Good);
        }

        private void OnCustomerHurried(Order order)
        {
            Say("Побыстрее,\nпожалуйста!");
            _board.ShowStatus("Клиент торопится!", CounterBoard.Bad);
        }

        private void OnOrderAbandoned(Order order)
        {
            PlaySound(FeedbackAudio.Sound.Error);
            Say("Всё, я ухожу!");
            _board.ShowHeadline("Клиент ушёл без заказа", CounterBoard.Bad);
            _board.ShowStatus($"Заказ №{SpokenNumber(order)} остался на складе", CounterBoard.Bad);
        }

        private void OnOrderCompleted(OrderResult result)
        {
            _step = TutorialStep.Off; // the player has done it once

            Say("Спасибо!\nХорошего дня!");
            _board.ShowHeadline("Заказ выдан", CounterBoard.Good);
            Celebrate();
            // ShiftStats subscribed to the desk before the presenter, so LastOrderPoints is already updated.
            _board.ShowStatus($"+{_shift.Stats.LastOrderPoints} очков", CounterBoard.Good);
        }

        private void RefreshScore() => _board.ShowScore(_shift.Stats.Score, _shift.Stats.OrdersCompleted, _shift.OrdersTotal, _clock);

        private void Celebrate()
        {
            if (_effects != null) _effects.Celebrate();
        }

        private void PlaySound(FeedbackAudio.Sound sound)
        {
            if (_audio != null) _audio.Play(sound);
        }

        private void Say(string line)
        {
            if (Customer != null) Customer.Say(line);
        }

        /// <summary>One wording for the box counter: «Коробок в заказе: N» at first, then «… осталось N».</summary>
        private static string BoxesLeftText(int left, int total)
        {
            if (total == 1) return "Найдите коробку";
            return left == total ? $"Коробок в заказе: {total}" : $"Коробок в заказе: осталось {left}";
        }
    }
}
