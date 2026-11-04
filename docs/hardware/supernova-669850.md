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

**Раніше (Skidata, прошивка 4.2.69)** та сама станція підключалась **двома** charge point: `WBX669850` (U008) і
`WBX669850#2` (U009), у кожного `Number of connectors = 1`. Дві можливі причини, **не розрізнено**:
(а) прошивка 4.2.x відкривала окреме з'єднання на роз'єм, а 4.6.16 — одне; (б) станція й тоді мала одне з'єднання,
а Skidata (модель «паркомісце = один роз'єм») сам розбив її на `WBX669850` і `WBX669850#2` — на це вказує
несиметричність ID (перший без `#1`). Перевірка: логи/WebSocket-URL у Skidata, або чи бачив сервер Skidata два
окремі Heartbeat. Ключа конфігурації для такого режиму в 4.6.16 немає (`SplitChargeConfiguration = Enabled` —
схоже, розподіл потужності між роз'ємами).

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

## Тест заряду — роз'єм 2 (17:50–17:52 за Києвом)

`AutoChargeEnabled` → `true` (ChangeConfiguration, **Accepted**; було `false` — **повернути**). Авто на роз'ємі 2 → `Preparing` 15:51:37Z,
станція почала старт сама → знову зрив:

| UTC | `vendorErrorCode` | `info` |
|---|---|---|
| 15:51:55 | `DCF:PMO:004:HWR:OD2` | Power module 4 HW component output diode error |
| 15:52:07 | `DCF:PMO:002:HWR:OF2` | Power module 2 HW component output fuse error |
| 15:52:24 | `DCF:CCS:002:ISO:TMO` / `DCF:CCS:002:SAF:000` | CCS hose 2 (timeout in) isolation test error |

Разом з першою спробою: несправні **три силові модулі** — 2 (вихідний запобіжник), 3 (вихідний запобіжник),
4 (вихідний діод). Кожна спроба старту задіює інший набір модулів, і щоразу один з них не видає напругу →
тест ізоляції CCS не проходить.

## Діагностика силових блоків по одному (18:29–19:08 за Києвом)

Щоразу в станції лише **один** блок; авто на роз'ємі 2. Станція нумерує модулі **за порядком виявлення, а не за
фізичним слотом**: єдиний блок завжди «Power module 1» (COM-помилки по 2–6), навіть в іншому слоті.

| Блок (маркування) | Помилка модуля | Тест ізоляції CCS hose 2 | Заряд |
|---|---|---|---|
| 3 | `PMO:001:HWR:OF2` output fuse | `ISO:TMO` → `SAF:000` | ❌ |
| 1 | `PMO:001:HWR:OF2` output fuse | `ISO:TMO` → `SAF:000` | ❌ |
| (третій) | станція `Faulted`, не вийшла в «готова» | — | ❌ не стартує |
| 4 | `PMO:001:HWR:OF2` output fuse | `ISO:TMO` → `SAF:000` | ❌ |
| 5 (інший слот) | `PMO:001:OVT:BAT` battery over voltage | `ISO:TMO` → `SAF:000` | ❌ |
| 6 | `PMO:001:HWR:OF2` output fuse | `ISO:TMO` → `SAF:000` | ❌ |

Помилка модуля — через ~16–30 с після `Preparing`. П'ять різних блоків → однакова поведінка ⇒ причина **не в блоках**, а в
спільному тракті станції після модулів (вимірювання вихідної напруги / DC-шина / вихідні контактори / IMD / щойно
замінений DC-лічильник). `DLS:002:TRI` = відкриті двері шафи; `PSY:001:HWR` = «Blackout» (знеструмлення).

## Чотири блоки разом (19:13–19:16 за Києвом)

Після Boot 19:13:22 станція бачить модулі 1–4 (COM-помилки лише по 5, 6). Авто на роз'ємі 2 → `Preparing` 19:14:39 →
19:15:10 `PMO:001:HWR:OF2` (output fuse) → 19:15:26 `CCS:002:ISO:TMO` → `SAF:000` → `Finishing`. Решта модулів у цій
спробі помилок не дали — станція падає на першому ж. 19:15:50 відчинено двері (`DLS:002:TRI`), 19:15:52 знеструмлено
(`PSY:001:HWR` «Blackout»). Останній Heartbeat 19:16:23; далі лише розриви з'єднання (19:18–19:34), з 19:34 станція
до EVOCPP не підключалась. Чи повернуто `AutoChargeEnabled = false` і `ConnectionUrl` — **не підтверджено**.

**Уточнення від користувача:** DC-лічильник не новий — знятий з іншої Supernova (там «несправні всі блоки», станція
була вимкнена) і **ніяк не прив'язаний** до цієї станції (серійний номер / адреса / налаштування в сервісному
інструменті не змінювались). Гіпотеза: станція спілкується з лічильником (помилки `PMD:COM` зникли), але не приймає
його вимірювання як достовірні → не бачить вихідної напруги → «output fuse/diode» на будь-якому модулі + таймаут тесту
ізоляції. Якщо так, блоки обох станцій можуть бути справні.
