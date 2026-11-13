# T007 — План

- [x] Core: резервирование заказов в `Shift` (`NextOrder` резервирует, `HasUnreservedOrders`, `ReleaseReservation`,
      освобождение при выдаче/уходе), `OrderGenerator` с исключениями; EditMode-тесты
- [x] Runtime: `Customer.GoTo` / `LeaveAndVanish`, очередь в `ShiftDirector` (места в линию, уход в сторону), `ShiftSettings.queueLength`
- [x] Пресеты: Лёгкая 1, Обычная 2, Сложная 3
- [x] PlayMode: двое в очереди с разными заказами, второй подходит после выдачи первому (12/12)
- [x] Скриншот `09_queue.png`
- [ ] Ревью, PR в `dev`