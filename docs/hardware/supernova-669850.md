# Wallbox Supernova 60 kW — SN 669850 (тестова, позичена)

Чужа DC-станція на ремонті; тимчасово підключена до Nabla Home EVOCPP для тестів (2026-11-04).

> **Повернути після тесту:** `ConnectionUrl` = `wss://ocpp.1up.energy/websocket/CentralSystemService/`,
> `ChargePointId` = `WBX669850` (не змінювався). Перед цим — `ClearChargingProfile`, якщо ставили профілі.
> У Nabla Home станція `wbx669850` створена з `manageLimit: false` — сервер профілів не надсилає.
> `ConnectionUrl` доступний на запис через OCPP (`ChangeConfiguration`) — повернути можна і віддалено.

## Паспорт (BootNotification)

| | |
|---|---|
| Vendor | Wall Box Chargers |
| Model | `DCF1-211-84600000` (PartNumber `DCF1-211-84600000-WB1-00-00-00`) |
| Серійний номер | 669850 |
| Прошивка | 4.6.16 |
| OCPP | 1.6J (пропонує лише `ocpp1.6`) |
| Лічильник | `Internal MID` |
| Роз'єми | `NumberOfConnectors = 2` — **одне з'єднання**, роз'єми як `connectorId` 1 і 2 (не окремі `…#1`/`…#2`) |

## Спостереження

- **Годинник стоїть на 2025-02-13** (усі `timestamp` у StatusNotification ~`2025-02-13T09:19Z` при реальній даті 2026-11-04).
  `currentTime` у відповіді на Boot не застосовує. Тому й дата помилки лічильників у локальному UI — «02/13/2025»:
  помилка актуальна, неправильний годинник. `TimeZone = Europe/Kyiv`.
- **Помилка DC-лічильників (DCF:PMD:001/002:COM:000, Degraded) в OCPP не видно**: роз'єми звітують
  `Available` / `NoError`, `vendorErrorCode` порожній.
- OCPP-Heartbeat не надходить (за 40 хв — жодного), хоча `HeartbeatInterval = 300`; з'єднання тримається
  WebSocket-пінгами (`WebSocketPingInterval = 50`), офлайн EVOCPP її не позначає.
- `MeterValueSampleInterval = 60` прийняла (EVOCPP ставить після Boot).

## Ключові параметри (повний список — [supernova-669850-config.json](supernova-669850-config.json), 99 ключів)

| Ключ | Значення | Примітка |
|---|---|---|
| `SupportedFeatureProfiles` | Core, LocalAuthListManagement, RemoteTrigger, Reservation, SmartCharging | без FirmwareManagement |
| `AuthorizeRemoteTxRequests` | false | RemoteStart без Authorize |
| `AuthEnabled` / `LocalAuthorizeOffline` / `AllowOfflineTxForUnknownId` | true / true / true | |
| `ChargingScheduleAllowedChargingRateUnit` | Current, Power | ліміт і в А, і у Вт |
| `ChargeProfileMaxStackLevel` | **0** | лише stackLevel 0 (EVOCPP і так ставить 0) |
| `ChargingScheduleMaxPeriods` | **1** | лише один період у розкладі |
| `MaxChargingProfilesInstalled` | 6 | |
| `ChargingWLimitConn0/1/2` | 60000 | вендорський ліміт потужності, Вт (на запис) |
| `MeterValuesSampledData` | Current.Import, Current.Offered, Energy.Active.Import.Register, Frequency, Power.Active.Import, Power.Factor, Power.Offered, **SoC**, Voltage | 9 з 10 |
| `MeterValueSampleInterval` | 60 | |
| `HeartbeatInterval` | 300 | |
| `ConnectionTimeOut` | 120 | с між підключенням авто та стартом |
| `StopTransactionOnEVSideDisconnect` | true | |
| `PublicKeyWithSignedMeterValue` / `SampledDataSignReadings` | Never / false | підписані покази вимкнено |
| `VehicleIDEnable` / `AutoChargeEnabled` | false / false | Autocharge (ідентифікація авто) вимкнено; ключів ISO 15118 немає |
| `PoSEnable` | Deactivated | платіжний термінал |
| `DailyRebootTime` | — | |
