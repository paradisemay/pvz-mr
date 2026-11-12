# T003 — План

- [x] Runtime: `QrBox`, `HandScanner`, `HandoverZone`, `Customer`, `SpeechBubble`, `CounterBoard`, `ShiftSummaryPanel`,
      `FeedbackAudio`, `RestartButton`, `ShiftDirector`, `MrSessionSetup`
- [x] `ShiftSettings` (ScriptableObject): коробки, темп, очки
- [x] Editor: QR-текстуры (декоративные), материалы, шрифт с кириллицей (`PvzSans SDF`), префабы
- [x] Editor: `PvzSceneBuilder.BuildAll` — сцена `Game.unity`, Build Settings
- [x] Editor: `PvzScreenshots.CaptureAll` — скриншоты в `docs/screenshots`
- [x] `tools/run-unity.ps1` — запуск методов Unity в batchmode без зависания на дочерних процессах
- [x] PlayMode-тесты (4/4): полная смена, коробки заранее в зоне, raycast сканера, возврат потерянной коробки
- [x] Ревью (код + архитектура), исправления — см. PR
- [x] PR в `dev`

## Заметки по реализации
- `NewScene(Single)` выгружает «неиспользуемые» ассеты → сцена создаётся до генерации префабов,
  префабы перезагружаются по пути после сохранения.
- Клиент идёт по прямой от точки появления (3,1 м за стойкой) — геометрия реальной комнаты игре неизвестна.
- На устройстве мок-комната выключается, остаются невидимый пол (y = 0) и коллайдеры плоскостей AR.
