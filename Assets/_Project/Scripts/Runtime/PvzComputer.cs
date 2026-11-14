using System;
using System.Text;
using Pvz.Core;
using TMPro;
using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// The pickup point's computer on the counter (T033, #77): a monitor and a row of physical keys that stays on the
    /// counter all the time. Each screen shows its own group of keys in the same slots, like the soft keys of a cash
    /// machine: the menu (difficulty, settings), the settings, the shift (current order, end shift) and the summary
    /// (new shift). Before, the start menu disappeared with the start of the shift and nothing could be changed until
    /// the end.
    /// </summary>
    public sealed class PvzComputer : MonoBehaviour
    {
        public enum Screen { Menu, Settings, Shift, Summary, Moving }

        [SerializeField] private PlacementMover counterMover;
        [SerializeField] private PlacementMover hatchMover;
        [SerializeField] private GameObject movingKeys;

        /// <summary>What is being moved on the moving screen (null otherwise).</summary>
        private PlacementMover _moving;

        internal void ConfigureMover(PlacementMover mover, GameObject keys)
        {
            counterMover = mover;
            movingKeys = keys;
        }

        internal void ConfigureHatchMover(PlacementMover mover) => hatchMover = mover;

        /// <summary>«Перенести стойку» (T034): the handle appears; «Применить» / «Отмена» end it.</summary>
        public void StartMovingCounter() => StartMoving(counterMover);

        /// <summary>«Место люка» (T036): the hatch is moved along the ceiling by a handle hanging from it.</summary>
        public void StartMovingHatch() => StartMoving(hatchMover);

        public void StartMoving(PlacementMover mover)
        {
            if (mover == null || Current != Screen.Settings) return;
            _moving = mover;
            mover.Begin();
            Show(Screen.Moving);
        }

        public void ApplyMove()
        {
            if (_moving == null || Current != Screen.Moving) return;
            _moving.Apply();
            _moving = null;
            Show(Screen.Settings);
        }

        public void CancelMove()
        {
            if (_moving == null || Current != Screen.Moving) return;
            _moving.Cancel();
            _moving = null;
            Show(Screen.Settings);
        }

        /// <summary>The moving screen for <paramref name="what"/> («стойку», «люк»).</summary>
        public static string MovingTextFor(string what) =>
            $"Возьмите оранжевую ручку хватом\nи перенесите {what}.\n\n«Применить» — оставить здесь,\n«Отмена» — вернуть на место.";

        public static string MovingText => MovingTextFor("стойку");

        [SerializeField] private ShiftDirector director;
        [SerializeField] private TMP_Text screenText;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private GameObject menuKeys;
        [SerializeField] private GameObject settingsKeys;
        [Tooltip("Settings that only make sense before the shift (moving the play area, resetting the warehouse).")]
        [SerializeField] private GameObject settingsBeforeShiftKeys;
        [SerializeField] private GameObject shiftKeys;
        [SerializeField] private GameObject summaryKeys;
        [SerializeField] private TMP_Text endShiftLabel;
        [SerializeField, Min(0.5f)] private float confirmSeconds = 3f;

        private Screen _beforeSettings = Screen.Menu;
        private float _confirmEndUntil = float.NegativeInfinity;
        private float _nextRefresh;

        public Screen Current { get; private set; } = Screen.Menu;
        public string ScreenText => screenText != null ? screenText.text : string.Empty;
        public bool IsConfirmingEnd => Time.unscaledTime < _confirmEndUntil;

        public event Action<Screen> ScreenChanged;

        private void OnEnable()
        {
            if (director == null) return;
            director.ShiftStarted += OnShiftStarted;
            director.ShiftEnded += OnShiftEnded;
        }

        private void OnDisable()
        {
            if (director == null) return;
            director.ShiftStarted -= OnShiftStarted;
            director.ShiftEnded -= OnShiftEnded;
        }

        private void Start() => Show(director != null && director.IsShiftOver ? Screen.Summary
            : director != null && director.IsStarted ? Screen.Shift : Screen.Menu);

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.25f;
            // The confirmation ran out: the key reads «Завершить смену» again, not «Точно?».
            if (endShiftLabel != null && !IsConfirmingEnd && endShiftLabel.text != EndShiftCaption) endShiftLabel.text = EndShiftCaption;
            RefreshText();
        }

        private void OnShiftStarted() => Show(Screen.Shift);
        private void OnShiftEnded() => Show(Screen.Summary);

        /// <summary>«Настройки»: from the menu, the shift or the summary; «Назад» returns there.</summary>
        public void OpenSettings()
        {
            if (Current != Screen.Settings) _beforeSettings = Current;
            Show(Screen.Settings);
        }

        public void Back()
        {
            if (Current == Screen.Settings) Show(_beforeSettings);
        }

        /// <summary>Between the start and the end of a shift (settings are limited then).</summary>
        private bool ShiftRunning => director != null && director.IsStarted && !director.IsShiftOver;

        /// <summary>«Завершить смену»: the first press asks to confirm, the second (within a few seconds) ends the shift.</summary>
        public void EndShift()
        {
            if (director == null || !director.IsStarted || director.IsShiftOver) return;
            if (!IsConfirmingEnd)
            {
                _confirmEndUntil = Time.unscaledTime + confirmSeconds;
                if (endShiftLabel != null) endShiftLabel.text = "ТОЧНО?";
                RefreshText();
                return;
            }
            _confirmEndUntil = float.NegativeInfinity;
            if (endShiftLabel != null) endShiftLabel.text = EndShiftCaption;
            director.EndShiftNow();
            RefreshText();
        }

        public const string EndShiftCaption = "ЗАВЕРШИТЬ\nСМЕНУ";

        public void Show(Screen screen)
        {
            // Leaving the moving screen another way (the shift ended meanwhile): keep the counter where it is now.
            if (screen != Screen.Moving && _moving != null && _moving.IsMoving)
            {
                _moving.Apply();
                _moving = null;
            }
            Current = screen;
            var shiftRunning = ShiftRunning;
            if (menuKeys != null) menuKeys.SetActive(screen == Screen.Menu);
            if (settingsKeys != null) settingsKeys.SetActive(screen == Screen.Settings);
            if (settingsBeforeShiftKeys != null) settingsBeforeShiftKeys.SetActive(screen == Screen.Settings && !shiftRunning);
            if (shiftKeys != null) shiftKeys.SetActive(screen == Screen.Shift);
            if (summaryKeys != null) summaryKeys.SetActive(screen == Screen.Summary);
            if (movingKeys != null) movingKeys.SetActive(screen == Screen.Moving);
            if (screen == Screen.Settings && shiftRunning) _beforeSettings = Screen.Shift;
            if (endShiftLabel != null && !IsConfirmingEnd) endShiftLabel.text = EndShiftCaption;
            RefreshText();
            ScreenChanged?.Invoke(screen);
        }

        private void RefreshText()
        {
            if (screenText == null) return;
            if (titleText != null) titleText.text = TitleFor(Current);
            screenText.text = Current switch
            {
                Screen.Menu => MenuText,
                Screen.Settings => ShiftRunning ? SettingsDuringShiftText : SettingsText,
                Screen.Shift => ShiftText(),
                Screen.Moving => MovingTextFor(_moving != null && _moving.Keeper != null ? _moving.Keeper.DisplayName : "стойку"),
                _ => SummaryText(),
            };
        }

        public static string TitleFor(Screen screen) => screen switch
        {
            Screen.Menu => "ПВЗ · НОВАЯ СМЕНА",
            Screen.Settings => "ПВЗ · НАСТРОЙКИ",
            Screen.Shift => "ПВЗ · СМЕНА",
            Screen.Moving => "ПВЗ · ПЕРЕНОС",
            _ => "ПВЗ · ИТОГИ",
        };

        public const string MenuText = "Выберите сложность смены\nкнопками ниже.\n\n«Настройки» — стойка, склад, тема.";
        public const string SettingsText = "«Перенести стойку» — взять за ручку и поставить.\n«Стойку ко мне» — поставить перед собой.\n«Тема» — оформление стойки.\n«Место люка» — откуда падают коробки.";
        public const string SettingsDuringShiftText = "«Перенести стойку» — взять за ручку и поставить:\nочередь перейдёт следом.\n«Тема» — оформление стойки.\n«Место люка» — откуда падают коробки.";

        private string ShiftText()
        {
            if (director == null || !director.IsStarted) return string.Empty;
            var desk = director.Desk;
            var session = desk.ActiveSession;
            return OrderScreen(
                session?.Order.Number, session?.Order.Boxes.Count ?? 0, session?.DeliveredCount ?? 0, session?.AwaitingBag ?? false,
                desk.HasActiveReturn ? desk.ActiveReturn.OrderNumber : null,
                Math.Max(0, director.QueuedVisits.Count - (desk.HasCustomer ? 1 : 0)),
                director.Shift.TimeLeft(Time.timeAsDouble), IsConfirmingEnd, desk.AwaitsCode);
        }

        /// <summary>The shift screen: the order at the counter, how many of its items are handed over, the line, the time.</summary>
        public static string OrderScreen(string orderNumber, int items, int delivered, bool awaitingBag, string returnNumber,
            int waiting, double? timeLeft, bool confirmingEnd, bool awaitingCode = false)
        {
            var text = new StringBuilder();
            if (confirmingEnd) text.Append("<color=#FF8A70>Нажмите ещё раз, чтобы\nзавершить смену</color>\n\n");
            // T035: before the code nothing about the customer is known.
            if (awaitingCode) text.Append("Клиент у стойки\nОтсканируйте код на его телефоне\n");
            else if (orderNumber != null)
            {
                text.Append("Заказ №").Append(orderNumber).Append('\n');
                text.Append("Товаров: ").Append(items).Append(" · выдано: ").Append(delivered).Append('\n');
                if (awaitingBag) text.Append("Нужен пакет\n");
            }
            else if (returnNumber != null) text.Append("Возврат по заказу №").Append(returnNumber).Append("\nОтсканируйте коробку\n");
            else text.Append("Ждём клиента\n");
            text.Append("В очереди: ").Append(waiting);
            if (timeLeft.HasValue)
            {
                var seconds = (int)Math.Ceiling(timeLeft.Value);
                text.Append(" · Время: ").Append(seconds / 60).Append(':').Append((seconds % 60).ToString("00"));
            }
            return text.ToString();
        }

        private string SummaryText()
        {
            if (director == null || !director.IsStarted) return string.Empty;
            return $"Смена завершена\nОчки: {director.Stats.Score}\nВыдано заказов: {director.Stats.OrdersCompleted}\n\n«Новая смена» — сыграть ещё.";
        }

        /// <summary>Shows a screen with a given text outside play mode (screenshots; the scene is set up as the menu).</summary>
        internal void Preview(Screen screen, string text, bool beforeShift)
        {
            if (menuKeys != null) menuKeys.SetActive(screen == Screen.Menu);
            if (settingsKeys != null) settingsKeys.SetActive(screen == Screen.Settings);
            if (settingsBeforeShiftKeys != null) settingsBeforeShiftKeys.SetActive(screen == Screen.Settings && beforeShift);
            if (shiftKeys != null) shiftKeys.SetActive(screen == Screen.Shift);
            if (summaryKeys != null) summaryKeys.SetActive(screen == Screen.Summary);
            if (movingKeys != null) movingKeys.SetActive(screen == Screen.Moving);
            if (titleText != null) titleText.text = TitleFor(screen);
            if (screenText != null) screenText.text = text;
            // Outside play mode text shown by activating its object is not laid out until something asks for it.
            foreach (var label in GetComponentsInChildren<TMP_Text>(false)) label.ForceMeshUpdate();
        }

        internal void Configure(ShiftDirector shiftDirector, TMP_Text screen, TMP_Text title, GameObject menu, GameObject settings,
            GameObject settingsBeforeShift, GameObject shift, GameObject summary, TMP_Text endShift)
        {
            director = shiftDirector;
            screenText = screen;
            titleText = title;
            menuKeys = menu;
            settingsKeys = settings;
            settingsBeforeShiftKeys = settingsBeforeShift;
            shiftKeys = shift;
            summaryKeys = summary;
            endShiftLabel = endShift;
        }
    }
}
