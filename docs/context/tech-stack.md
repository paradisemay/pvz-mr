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
# тесты
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode.xml
Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/playmode.xml
```
`Unity.exe` = `C:\Program Files\Unity\Hub\Editor\6000.2.2f1\Editor\Unity.exe`. Редактор с этим проектом должен быть закрыт.
