# T005 — План

- [x] Core: `Order.NeedsBag`, шанс пакета в `OrderGenerator`/`Shift`, `ServiceSession.AwaitingBag/BagDelivered`,
      `PickupDesk.HandOverBag` → `BagStatus`, событие `BagHandedOver`, очки за пакет, `ShiftStats.BagsGiven`
- [x] EditMode-тесты на пакет и многопредметный заказ (91/91 всего)
- [x] Runtime: `ParcelBag`, `BagDispenser`, `HandoverZone.BagEntered`, стопка коробок у клиента, тексты в презентере
- [x] Данные: BOX-101 + BOX-102 → заказ 3307 из двух коробок; шанс пакета 50 %
- [x] Editor: префаб пакета, раздатчик на стойке, скриншот `08_bag_request.png`
- [x] PlayMode: полная смена — 5 заказов (один из двух коробок), 5 пакетов, поставка (5/5 тестов)
- [ ] Ревью, PR в `dev`
