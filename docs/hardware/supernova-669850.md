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
  дата в UI — це неправильний годинник, не давність помилки. `TimeZone = Europe/Kyiv`.
- Помилку DC-лічильників (DCF:PMD:001/002:COM:000) користувач **усунув заміною лічильника до підключення**
  до EVOCPP, тому `Available` / `NoError` — справжній стан.
- Heartbeat приходить (перший о 17:38:40 після Boot о 17:33:45); з'єднання додатково тримається
  WebSocket-пінгами (`WebSocketPingInterval = 50`).
- `TriggerMessage(StatusNotification)` → `Accepted`, статуси роз'ємів 0/1/2 — `Available` / `NoError`.
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

## Тест заряду 2026-11-04 (Tesla, CCS, роз'єм 1)

Роз'єм 1 `Preparing` 15:45:57Z → `RemoteStart` (EVHomeAPI, idTag `U1`) **Accepted** 15:46:29Z → через ~40 с старт зірвався:

| UTC | Роз'єм | `errorCode` / `vendorErrorCode` | `info` |
|---|---|---|---|
| 15:47:07 | 1 (Preparing), 2 (Available) | OtherError / `DCF:PMO:003:HWR:OF2` | Power module 3 HW component output fuse error |
| 15:47:23 | 1 → Finishing | OtherError | |
| 15:47:24 | 1 | OtherError / `DCF:CCS:001:ISO:TMO` | CCS hose 1 timeout in isolation test error |
| 15:47:25 | 1 | OtherError / `DCF:CCS:001:SAF:000` | CCS hose 1 isolation test error |

`StartTransaction` не було; `charger.remote.start.response` опубліковано о 15:47:30Z.

**Висновки**
- Апаратні помилки станція **передає в OCPP**: `errorCode = OtherError`, код Wallbox у `vendorErrorCode`
  (той самий формат, що в локальному UI: `DCF:<блок>:<номер>:<тип>:<код>`), людський текст в `info`.
- Годинник після підключення синхронізувався: з 15:43:47Z timestamps правильні.
- Ланцюжок: вихідний запобіжник силового модуля 3 → DC-вихід не набирає напругу для тесту ізоляції
  CCS → таймаут тесту ізоляції → старт скасовано.
