# T001 — Подготовка проекта

## Зачем
Папка — бывший проект «Escape room». Нужна чистая база под MR-прототип ПВЗ с сохранением настроенных XR-пакетов.

## Критерии приёмки
- Контент эскейп-рума удалён (скрипты, модели ~2 ГБ, сцены, звуки, инвентарь, старые APK ~3,6 ГБ).
- Добавлены AR Foundation 6.5.1 и Unity OpenXR: Meta 2.5.1; проект импортируется в batchmode без ошибок.
- Для Android в OpenXR включены фичи Meta: Session, Camera (passthrough), Planes, Anchors; Meta Quest Support.
- Player Settings: имя продукта «ПВЗ MR», id `com.pvzmr.prototype`, IL2CPP, ARM64.
- Созданы asmdef-сборки `Pvz.Core`, `Pvz.Runtime`, `Pvz.Editor`, тестовые сборки.
- Настройки применяются кодом (`PvzProjectSetup.Apply`) — воспроизводимо.
