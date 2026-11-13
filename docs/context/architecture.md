# Architecture guidelines

## Принципы
1. **Логика отдельно от Unity.** Правила игры (что такое заказ, правильная ли коробка, сколько очков) живут в `Pvz.Core`
   без ссылок на `UnityEngine` и покрываются EditMode-тестами. MonoBehaviour-ы — тонкие адаптеры.
2. **Зависимости в одну сторону:** `Editor → Runtime → Core`. Core ничего не знает о сцене.
3. **События вместо прямых вызовов между фичами.** Источник события — Core (`PickupDesk.OrderCompleted`,
   `PickupDesk.WrongHandover`, ...). UI, звук, клиенты подписываются; `ShiftDirector` — единственный, кто их связывает.
4. **Сцена собирается кодом.** `PvzSceneBuilder.BuildAll` (с `PvzPrefabBuilder` и `PvzAssetFactory`) создаёт префабы,
   материалы, QR-текстуры, пресеты сложности и сцену `Game.unity`. Ручные правки допустимы, но всё, что нужно для
   воспроизводимости, — в билдере.
5. **Сканирование отделено от Core.** Игровая логика получает `BoxId`, а не «кадр камеры»: `HandScanner` сообщает
   `BoxScanned(QrBox)` (raycast по `QrBox`), директор передаёт `Shift.Scan(id)`. Распознавание камерой позже
   подставляется на месте `HandScanner`, не трогая Core.
6. **Нет синглтонов и `FindObjectOfType` в рантайме.** Ссылки — через сериализованные поля, которые проставляет билдер.
7. **Детерминизм для тестов.** Случайность — через `IRandomSource`, время — параметром (`now`), а не `Time.time` внутри Core.

## Решения, принятые по итогам ревью T003
- **Правила смены — в Core (`Shift`)**: какой заказ следующий, когда смена окончена, какие коробки из зоны
  выдачи отдать пришедшему клиенту. `ShiftDirector` — только композиция, темп (корутины) и проброс ввода.
- **Все тексты и звуки — в `ShiftPresenter`**, который подписан на события Core. Новая фича = новое событие
  в Core + реакция в презентере, а не ещё один `switch` в директоре.
- **Раскладка коробок — в `ShiftSettings`** (id, номер заказа, стартовая позиция) — единый источник правды для
  билдера сцены и для логики.
- **Билдер не затирает работу художника**: существующие материалы не перезаписываются. Когда появятся
  настоящие модели — художник делает их отдельными префабами в `Art/`, а билдер переводится в режим
  «проверить и связать» (тест `SceneIntegrityTests` ловит неназначенные ссылки после ручных правок).
- **На шлеме всё игровое — под `Play Area`**, который выравнивается по позиции и взгляду игрока; симулятор и
  мок-комната помечены `EditorOnly` и не попадают в сборку.

## Решения T004–T006
- Расписание поставки, приёмка, пакет и терпение клиентов — правила Core (`SupplyPlan`, `Shift.Tick`,
  `ServiceSession.AwaitingBag`); директор только спавнит объекты и пробрасывает ввод.
- Каждая новая реакция игрока = событие Core (`BoxReceived`, `BagRequested`, `CustomerHurried`, `OrderAbandoned`)
  + обработчик в `ShiftPresenter`.
- Пресет сложности — отдельный `ShiftSettings`; раскладка склада в пресетах одинакова (проверяется тестом).
- Отложено осознанно: явный список заказов со статусами (для очереди клиентов), обобщённый `ExtraRequest`,
  `DifficultyPreset` поверх одной раскладки, `GameFlow` для фаз «меню → смена → итоги».

## Решения T007–T011
- Очередь клиентов — правило Core: упорядоченная линия зарезервированных заказов в `Shift` (`TryEnqueue` с длиной
  очереди, `OpenNext`, `LeaveQueue`); директор хранит только «какой клиент на каком месте». Полный список заказов со
  статусами по-прежнему отложен (нужен для терпения в очереди и нескольких стоек).
- Разрешение `USE_SCENE`: `ARPlaneManager` сохранён в сцене выключенным, `ScenePermissionGate` включает его после согласия.
- Рекорды: правило в Core (`BestScores` → `RecordResult`), хранилище — интерфейс `IScoreStore`
  (`PlayerPrefsScoreStore` в Runtime, `MemoryScoreStore` в тестах); ключ — стабильный id пресета (имя ассета).
- Полоска терпения: `Shift.PatienceRemaining` → `ShiftPresenter.UpdatePatience` → `PatienceBar`; цвета привязаны к `Shift.HurryFraction`.
- Подсказки первого заказа — шаги «только вперёд» в `ShiftPresenter` (`ShiftSettings.showTutorial`).

## Решения T013 (возвраты)
- Очередь состоит из `Visit` с явным `VisitKind` (`Pickup` / `Return`); новый вид доп. запроса — новый `VisitKind`,
  а резерв склада идёт только через `Visit.ReservedOrderNumber`, без проверок «а не возврат ли это».
- `PickupDesk` обслуживает одного клиента: заказ **или** возврат (`HasCustomer`); `OpenReturn` / `ReturnAccepted`.
  Единый `ActiveVisit` на стойке и явный список заказов со статусами — следующий шаг (T014), когда видов станет больше двух.
- Проданная коробка может вернуться только возвратом: её id запоминается в `Shift` и отклоняется в поставках.
  Скан коробки активного возврата проверяется раньше приёмки поставки.
- Директор не ветвится по виду визита: коробку возврата кладёт обработчик `Desk.ReturnOpened` на
  `HandoverZone.ReturnSpot` (рядом с зоной, не внутри — не задевает подготовленные коробки). Без префаба коробки
  возвраты выключаются с ошибкой в логе — бесплатных очков «по скану ничего» нет.

## Модули
```
Core/
  BoxId, BoxRecord   коробка: id "BOX-001" + номер заказа
  BoxCatalog         склад (добавить/выдать/найти)
  Order              неизменяемый заказ: номер, коробки, нужен ли пакет
  OrderGenerator     случайный заказ из склада (с исключениями — для очереди), шанс пакета
  ServiceSession     обслуживание одного клиента: выдано, ошибки, пакет, «торопил ли»
  Visit, ReturnRequest  визит клиента в очереди: заказ или возврат проданной коробки
  PickupDesk         стойка: OpenOrder / OpenReturn / Scan / Handover / HandOverBag / Abandon → исходы + события
  Outcomes           ScanOutcome, HandoverOutcome, BagStatus, OrderResult
  Shift              смена: склад, очередь, поставка, терпение (Tick), конец смены
  SupplyPlan         запланированная поставка
  ShiftStats         очки и статистика; ScoringRules — таблица очков; ShiftRating — оценка смены
  BestScores         рекорды по сложностям (IScoreStore, MemoryScoreStore, RecordResult)
  QrPattern          узор QR по id (общий для редактора и рантайма)
  IRandomSource      случайность для детерминированных тестов
Runtime/
  ShiftDirector      композиция и темп смены, очередь клиентов на местах, проброс ввода в Core
  ShiftPresenter     все тексты/звуки/реплики/подсказки по событиям Core
  ShiftSettings      пресет сложности: склад, поставка, темп, очки, терпение, очередь, подсказки
  BoxRegistry, QrBox, QrTextures   коробки сцены, рантайм-QR-текстуры
  HandScanner        ручной сканер (XRGrabInteractable, курок → raycast → QrBox)
  HandoverZone       триггер на стойке: коробки и пакеты
  Customer, SpeechBubble, PatienceBar, Billboard   клиент, реплики, полоска терпения, поворот к игроку
  CounterBoard, ShiftSummaryPanel  табло и итоги смены
  SupplyDrop         коробки поставки из-под потолка в досягаемых точках
  ParcelBag, BagDispenser          пакет и раздатчик на стойке
  DifficultyButton, RecenterButton, RestartButton   кнопки на стойке
  MrSessionSetup, ScenePermissionGate   шлем/редактор, Play Area, разрешение USE_SCENE
  FeedbackAudio, PlayerPrefsScoreStore  процедурные звуки, хранение рекордов
Editor/
  PvzSceneBuilder, PvzPrefabBuilder, PvzAssetFactory   генерация сцены, префабов, материалов, QR, шрифта, пресетов
  PvzProjectSetup    OpenXR-фичи Meta, Player Settings, исправления валидации
  PvzBuild           сборка APK для Quest
  PvzScreenshots     скриншоты для PR (с целью Standalone)
  PvzTestRunner      запуск тестов через TestRunnerApi из -executeMethod
```

## Стиль кода
- C# 9, `namespace Pvz.Core` / `Pvz.Runtime` / `Pvz.Editor`.
- `[SerializeField] private` вместо публичных полей; публичные свойства только на чтение.
- Тексты для игрока — на русском, идентификаторы и комментарии к коду — на английском.
- Без магических чисел в логике: параметры смены — в `ShiftSettings` (ScriptableObject).
