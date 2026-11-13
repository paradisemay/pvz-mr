# Tech stack

| Слой | Технология | Версия |
|---|---|---|
| Движок | Unity | 6000.2.2f1 |
| Рендер | URP (Mobile_RPAsset для Android) | 17.2.0 |
| XR runtime | OpenXR + XR Plug-in Management | 1.15.1 / 4.6.0 |
| MR-функции | AR Foundation + Unity OpenXR: Meta (passthrough, плоскости, якоря) | 6.5.1 / 2.5.1 |
| Взаимодействие | XR Interaction Toolkit + Starter Assets + XR Interaction Simulator | 3.2.2 |
| Ввод | Input System | 1.14.2 |
| UI | uGUI + TextMeshPro (world-space) | 2.0.0 |
| Тесты | Unity Test Framework (EditMode + PlayMode) | 1.5.1 |
| Платформа | Android, ARM64, IL2CPP, Meta Quest 3/3S | — |

## Сборки (assembly definitions)
| Сборка | Путь | Зависит от | Назначение |
|---|---|---|---|
| `Pvz.Core` | `Assets/_Project/Scripts/Core` | — (`noEngineReferences`) | Чистая игровая логика: коробки, заказы, стойка, статистика |
| `Pvz.Runtime` | `Assets/_Project/Scripts/Runtime` | Core, XRI, AR Foundation, TMP | MonoBehaviour-адаптеры: сцена, ввод, UI |
| `Pvz.Editor` | `Assets/_Project/Scripts/Editor` | Core, Runtime | Генерация сцены/префабов/QR-текстур, скриншоты |
| `Pvz.Core.Tests` | `Assets/_Project/Tests/EditMode` | Core | Юнит-тесты логики |
| `Pvz.Runtime.Tests` | `Assets/_Project/Tests/PlayMode` | Core, Runtime | Сквозной тест игрового цикла в сцене |

## Командная строка
```bash
# импорт / компиляция
Unity.exe -batchmode -quit -projectPath . -buildTarget Android -logFile Logs/batch.log
# пересобрать сцену и ассеты прототипа
Unity.exe -batchmode -quit -projectPath . -executeMethod Pvz.Editor.PvzSceneBuilder.BuildAll -logFile Logs/build_scene.log
# применить настройки проекта (OpenXR-фичи Meta, Player Settings, URP)
Unity.exe -batchmode -quit -projectPath . -executeMethod Pvz.Editor.PvzProjectSetup.Apply -logFile Logs/setup.log
# скриншоты для PR (обязательно с целью Standalone: под Android Unlit-шейдеры в редакторе рендерятся заглушкой)
./tools/run-unity.ps1 -Method Pvz.Editor.PvzScreenshots.CaptureAll -Extra '-buildTarget','Win64'
# APK для Quest
./tools/run-unity.ps1 -Method Pvz.Editor.PvzBuild.BuildQuestApk -NoQuit -Extra '-buildTarget','Android'
# тесты (без -quit: редактор сам завершится с кодом 0/1)
Unity.exe -batchmode -projectPath . -executeMethod Pvz.Editor.PvzTestRunner.RunEditMode -pvzResults Logs/editmode.xml -logFile Logs/editmode.log
Unity.exe -batchmode -projectPath . -executeMethod Pvz.Editor.PvzTestRunner.RunPlayMode -pvzResults Logs/playmode.xml -logFile Logs/playmode.log
```
Стандартный `-runTests` на машине разработчика не стартует без headless-лицензии, поэтому тесты запускаются
через `PvzTestRunner` (обёртка над `TestRunnerApi`).
`Unity.exe` = `C:\Program Files\Unity\Hub\Editor\6000.2.2f1\Editor\Unity.exe`. Редактор с этим проектом должен быть закрыт.
