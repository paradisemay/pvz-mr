# T006 — План

- [x] Core: терпение (`Shift.PatienceSeconds`, `Tick`, `CustomerHurried`, `OrderAbandoned`), `PickupDesk.Abandon`,
      штраф `AbandonPenalty`, `ShiftStats.OrdersAbandoned`, рейтинг учитывает ушедших
- [x] EditMode-тесты (104/104 всего): торопит один раз, уход, заказ остаётся на складе, бесконечное терпение, пресеты сложности
- [x] Runtime: `DifficultyButton`, смена стартует по выбору (`ShiftDirector.Begin`), `Update → Shift.Tick`, реплики в презентере
- [x] Editor: пресеты `ShiftSettings` (Лёгкая / Обычная / Сложная), кнопки на стойке, скриншот `00_difficulty_menu.png`
- [x] PlayMode (11/11): меню ждёт выбора, кнопка «Сложная» запускает сложную смену, «Лёгкая» — без поставки и пакетов, нетерпеливый клиент уходит
- [ ] Ревью, PR в `dev`