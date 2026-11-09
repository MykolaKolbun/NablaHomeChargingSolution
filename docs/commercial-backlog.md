# Commercial Backlog

Ideas and fixes discovered while building Nabla Home that may also benefit the commercial
solution (`..\NablaPublicChargingSolution`). Nothing here is applied from this repo —
each item is applied later, separately, in the commercial repo it targets.

Format: one section per item. Status: `idea` → `planned` → `applied (commit/PR)` / `rejected`.

---

## CB-001 · Configurable RabbitMQ VirtualHost and CORS origins in OCPPServer
- **Target:** `OCPP_RD/OCPPServer` (`Program.cs`, `RabbitMqPublisher.cs`)
- **Status:** idea
- **Why:** `docs/messaging-api-spec.md` §6 already documents `RabbitMq:VirtualHost`, but the code
  ignores it (always `/`). CORS origin `https://admin.alternatiview.com.ua` is hard-coded.
  Making both configurable (defaults = current values) allows staging/test deployments on a
  shared broker without code changes.
- **Reference implementation here:** `EVOCPP` commit `8130e2e`.

## CB-002 · EVChargingApi.Tests no longer compiles
- **Target:** `EVChargingApi.Tests/Fiscal/FiscalFlowTests.cs`
- **Status:** idea
- **Why:** 28 compile errors — tests use `PaymentOrder.FiscalStatus`, `FiscalAttempts`,
  `FiscalReceiptId`, `FiscalReceiptUrl`, `FiscalIssuedAt`, which were removed from the model.
  The whole test project is blocked, so no API tests run at all.
- **Found:** 2026-10-08, while verifying the folder move.

## CB-003 · Migration AddMeterStartWhToPlug is invisible to EF (fresh DB breaks)
- **Target:** `OCPP_RD/OCPPServer/Migrations/20260609120000_AddMeterStartWhToPlug.cs`
- **Status:** idea
- **Why:** the migration has no Designer file → no `[DbContext]`/`[Migration]` attributes → EF
  never discovers it. Production `ocppdb` is fine (column exists, history row present), but any
  **fresh** database (new server, disaster restore from schema, test env) is created without
  `Plugs.MeterStartWh`, and every `Plugs` query fails with `42703: column p.MeterStartWh does not exist`.
  Same pattern on `RemoveMeterStartStopFromConnector` and `ErrorLogsLocalTime` — those two are
  intentionally disabled (see OCPP_RD `b9d85b3`) and must stay so, but deserve a comment in the file.
- **Reference implementation here:** EVOCPP — attributes declared in the migration file,
  `ADD COLUMN IF NOT EXISTS` for idempotency.
- **Found:** 2026-10-08, first deploy of EVOCPP on an empty database.

## CB-004 · Remove self-hosted Pi workflow from EVChargingAdmin
- **Target:** `EVChargingAdmin/.github/workflows/deploy-pi.yml` (`runs-on: self-hosted`)
- **Status:** idea
- **Why:** the self-hosted runner `AlternativiewHostPI4` on the Pi was removed on 2026-10-08
  (building on the Pi was too heavy; runner idle since 2026-06-05, cost ~60 MB RAM + 1.5 GB disk).
  Without a runner every push to `master` leaves this job queued forever. `deploy.yml`
  (`ubuntu-latest` + QEMU arm64) already covers build and deploy.
- **Found:** 2026-10-08, Pi memory review.

## CB-005 · Destructive button is red-on-red in light theme
- **Target:** `EVChargingApp/src/components/ui/Button.tsx` (`destructive` variant), `src/theme/colors.ts` (`LightColors.stopText`)
- **Status:** idea
- **Why:** light theme fills the button with `stopBorder` `#EF4444` and writes `stopText` `#DC2626` on it —
  contrast 1.28:1, label unreadable (seen on a real phone in Nabla Home's stop confirmation, which uses the copied Button).
  Dark theme is fine (5.28:1).
- **Reference implementation here:** `EVHomeApp` — destructive = `stopBg` fill + `stopBorder` border + `stopText`;
  light `stopText` → `#B91C1C`. Contrast: light 5.30:1, dark 8.40:1.
- **Found:** 2026-10-09, first device test of EVHomeApp.

## CB-006 · RemoteStart timeout is never published
- **Target:** `OCPP_RD/OCPPServer/OcppCommandHandler.cs` (`command.remote.start`, `catch (OperationCanceledException)`)
- **Status:** idea
- **Why:** when the charger accepts RemoteStartTransaction but no StartTransaction arrives within
  `Ocpp:TransactionConfirmTimeoutSeconds`, only a warning is logged — `charger.remote.start.response`
  with `Timeout` (promised by messaging-api-spec §3.6) is never sent. RemoteStop does publish its Timeout.
  The API keeps the session Pending until its own watchdog fires.
- **Reference implementation here:** EVOCPP — publish `Timeout` in the catch block.
- **Found:** 2026-10-09, while adding charging-limit commands.

## CB-007 · Power loss mid-session is reported as a normal stop
- **Target:** `OCPP_RD/OCPPServer/OCPP1.6 Models/Communicator.cs` (`HandleStopTransaction`), `RabbitMqPublisher.PublishTransactionStoppedAsync`; consumer in EVChargingApi
- **Status:** applied — OCPP_RD `f8819da` (2026-11-07), EVChargingApi `d0b3b56` (2026-11-07)
- **Why:** `StopTransaction.reason` is dropped, so a session cut by a power outage (`PowerLoss`,
  sent after the charger reboots — observed on Wallbox Copper SB) looks like a regular stop and its
  end time is when the queued message arrived (hours later), not when charging stopped. Commercial
  could at least record the reason / use the StopTransaction `timestamp`; auto-resume is a product
  decision (billing implications).
- **Reference implementation here:** EVOCPP passes `reason` in `charger.transaction.stopped` and publishes
  `charger.booted`; EVHomeAPI `SessionResumeService` (pause + RemoteStart under the same session).
- **Found:** 2026-10-09, grid outage during a Wallbox test session.

## CB-008 · Wallbox Supernova: real OCPP behaviour (observed on a live unit)
- **Target:** OCPP_RD (connection / StatusNotification / RemoteStart handling, Supernova adapter),
  EVChargingApi (station model, sessions, operator UI), Supernova onboarding (PR-011)
- **Status:** partial — DataTransfer VehicleID autostart applied (OCPP_RD `f8819da`, EVChargingApi `d0b3b56` 2026-11-07); items 1–6 tracked by CB-011 (applied), item 8 (energy cross-check) open
- **Source:** Wallbox Supernova 60 kW DC SN 669850, model `DCF1-211-84600000`, fw 4.6.16, connected to
  Nabla Home EVOCPP on 2026-11-04 during a repair. Everything below is what the charger itself sent —
  not vendor docs, not another backend's interpretation.
- **What to apply:**
  1. **One charge point, two connectors.** It opens ONE WebSocket as `WBX669850` (= `ChargePointId`),
     `NumberOfConnectors = 2`, connectors arrive as `connectorId` 1 and 2 (0 = station). Model it as one
     station with two EVSEs — not two stations `…#1`/`…#2`. The `#1`/`#2` ids seen earlier under Skidata
     were almost certainly Skidata's per-parking-space split (asymmetric: `WBX669850` + `WBX669850#2`).
     Every status, RemoteStart and session must carry the connectorId.
  2. **Hardware faults come over OCPP.** StatusNotification with `errorCode = OtherError`, Wallbox code
     in `vendorErrorCode` (`DCF:<block>:<nnn>:<type>:<code>`, same as its local UI) and English text in
     `info`. Store and show `vendorErrorCode` + `info` to the operator; alert on them. Codes seen:
     `PMO:nnn:HWR:OF2` power-module output fuse, `PMO:nnn:HWR:OD2` output diode — **if every module reports
     OF2/OD2, the cause is the common DC output fuse F6** (confirmed by Wallbox, case 01213780: the controller
     sees module voltage but 0 V on the DC bus) — operator hint: "check F6", not "replace modules", `PMO:nnn:OVT:BAT`
     battery over-voltage, `PMO:nnn:COM` module missing, `CCS:00n:ISO:TMO` / `CCS:00n:SAF:000` CCS
     isolation test timeout / failure, `PMD:00n:COM:000` DC meter comms, `DLS:002:TRI` cabinet door open,
     `PSY:001:HWR` "Blackout" (mains lost). Power modules are numbered by discovery order, not slot.
  3. **Failed DC start = Accepted RemoteStart without StartTransaction.** RemoteStart → `Accepted`, then
     ~16–40 s later a module error, `ISO:TMO` → `SAF:000`, connector → `Finishing`, no StartTransaction.
     Backend should end the pending session on that fault (not wait for its timeout) and tell the user
     why (related: CB-006).
  4. **Don't trust its clock.** After boot its timestamps were `2025-02-13…` (RTC lost); later in the same
     connection they became correct. Use server receive time when a timestamp is far off.
  5. **Smart charging limits.** `ChargeProfileMaxStackLevel = 0`, `ChargingScheduleMaxPeriods = 1`,
     `MaxChargingProfilesInstalled = 6`, units Current and Power — for DC send the limit in **W**, one
     period, stackLevel 0. Vendor keys `ChargingWLimitConn0/1/2` (W, writable, 60000).
  6. **Useful config facts.** `AuthorizeRemoteTxRequests = false` (RemoteStart needs no Authorize);
     `AutoChargeEnabled` (start on plug-in, default false); `ConnectionUrl` and `ChargePointId` writable via
     ChangeConfiguration (remote migration between backends); `MeterValuesSampledData` includes SoC,
     Power.Active.Import, Voltage, Current; `HeartbeatInterval = 300` + WebSocket ping 50 s;
     `ConnectionTimeOut = 120`; `StopTransactionOnEVSideDisconnect = true`; OCPP 1.6 only.
  7. **ISO 15118 / Autocharge by EVID — field data for PR-003** (2026-11-05, with `AutoChargeEnabled = true`):
     `Authorize {"idTag":"VID:98ed5cdc5016"}` (VID: + 12 hex = EVCCID) 6 s after `Preparing`; backend answered in
     94 ms (10 s window is ample); `StartTransaction` 35 s later with the same idTag and the real `connectorId`
     (Authorize has none — Gap A). **Gap B is real:** with `AuthorizationCacheEnabled = true` and
     `LocalPreAuthorize = true` a once-accepted VID starts later sessions with **no Authorize at all** (seen twice);
     after `ClearCache` it asks again. Gap C closed for Wallbox (`VID:` prefix used). Onboarding should either
     disable the cache / LocalPreAuthorize or handle `StartTransaction` with an unauthorized VID (wallet check).
     RemoteStopTransaction works for Autocharge sessions; StopTransaction carries an empty idTag.
  8. **Energy register unreliable on this unit** (both DC meters): power/current/SoC are live, but
     `Energy.Active.Import.Register` barely moves in MeterValues and jumps at StopTransaction — 2–3× under-count.
     A billing backend should cross-check energy against ∫power and flag sessions where they diverge.
  9. **`DataTransfer` "VehicleID" must get a CALLRESULT `{"status":"Accepted"}`** (Wallbox, case 01213780,
     2026-11-09). With an empty payload, an unknown messageId, a CALLERROR such as `NotSupported`, or a timeout, the
     charger keeps retransmitting it (the ~20 s repeat we saw). Check that OCPP_RD `f8819da` answers `Accepted`.
     Answer other vendors' DataTransfer with a CALLRESULT too (`UnknownVendorId` / `UnknownMessageId`), not a CALLERROR.
- **Reference here:** `docs/hardware/supernova-669850.md` (all event timelines) and
  `docs/hardware/supernova-669850-config.json` (full GetConfiguration, 99 keys).
- **Found:** 2026-11-04 / 2026-11-05.

## CB-009 · Limit losses when a charger loses the backend mid-session
- **Target:** OCPP_RD (per-session config writes), EVChargingApi (prepaid sessions, offline policy)
- **Status:** idea — needs Supernova test T1/T2 first
- **Why:** if the connection drops, RemoteStop cannot reach the charger; a prepaid session keeps charging
  until the car is full. Options (Wallbox vendor keys, see CB-008):
  1. **Prepaid:** at start, `ChangeConfiguration MaxTransactionEnergy` / `EnergyLimitToStopCharging` =
     paid + small tolerance — the charger stops by itself offline. One write per session.
  2. **Dead-man timer** (sessions without an energy cap): while the connection is alive, periodically extend
     `MaxTransactionDuration` to `elapsed + margin`; lost connection ⇒ charger stops within the margin
     (60 kW × 2 min ≈ 2 kWh). Extend on a successful ChangeConfiguration reply, not on MeterValues; keep the
     write rate low (flash wear: e.g. every 5 min to +10 min); the key is per station ⇒ use the oldest transaction.
  3. **Offline authorization is the bigger hole:** Supernova ships with `LocalAuthorizeOffline = true`,
     `AllowOfflineTxForUnknownId = true` — offline, anyone can start (capped only by
     `MaxEnergyOnInvalidId = 5000`). Decide the public-charger policy and set it at onboarding.
  Unknown until tested: units, whether a change applies to a running transaction, StopTransaction `reason`.
- **Reference here:** `docs/hardware/supernova-669850.md` → «План тестів» T1/T2.
- **Found:** 2026-11-05, design discussion during Supernova repair.

## CB-010 · Authorize race: a fast backend answer is dropped and the vehicle is rejected
- **Target:** `OCPP_RD/OCPPServer/OCPP1.6 Models/Communicator.cs` (`HandleAuthorize`, lines ~337–339),
  `OCPP2.1 Models/Ocpp21Communicator.cs` (`HandleAuthorize`)
- **Status:** idea
- **Why:** `PublishAuthorizeRequestedAsync` is fired **before** `RegisterPendingAuthorize`. When the backend answers
  within the same millisecond, `CompletePendingAuthorize` finds no waiter, the answer is lost, and 10 s later the
  vehicle is rejected ("backend timeout"). Seen on a real Supernova (2026-11-05): `command.authorize.response:
  Accepted` logged, then timeout, charger shows `DCF:USM:001:COM:TMO` "User Authentication Timeout"; three
  attempts in a row. Matters as soon as `AutoStart:Iso15118Enabled` is switched on (PR-003).
- **Reference implementation here:** EVOCPP — register the waiter first, then publish.
- **Found:** 2026-11-05.

## CB-011 · Multi-connector stations: rework from "one OCPP id per connector" to "one id, N connectors"
- **Target:** OCPP_RD (`ChargingStationConnections` / `ConnectorState`, `Communicator.cs` handlers,
  RabbitMQ events), EVChargingApi (station/connector model, sessions, wallet guard, app), PR-011
- **Status:** applied (backend) — OCPP_RD + EVChargingApi correctness complete; app connector picker + OCPP_RD per-connector DB status open
- **Why:** the commercial design assumed each connector of a multi-connector charger connects with its own OCPP id
  (`U005#1`, `U005#2`). That came from Skidata, which splits a station into parking spaces itself. A real Wallbox
  Supernova (fw 4.6.16) connects as **one** charge point `WBX669850` with `NumberOfConnectors = 2`, and its two
  connectors arrive as `connectorId` 1 and 2. Today OCPP_RD keeps **one state per OCPP id** and ignores `connectorId`
  in MeterValues (`HandleMeterValueNotification`, `Communicator.cs` ~491–523). With two cars on 2026-11-05 the
  two connectors' meters interleaved under one id every few seconds:
  `c2 28 268 884 Wh`, `c1 19 107 884 Wh`, `c2 28 269 208 Wh`.
  The result would be ±9 MWh "deltas", a mixed-up MeterStartWh, wrong power and the other car's SoC.
- **What to change**
  1. State keyed by **(ocppId, connectorId)**: socket/boot per station, but session, meter, SoC, status, CarId,
     pending start/stop per connector. connectorId 0 = station-level status and errors.
  2. Route StartTransaction / MeterValues / StatusNotification by `connectorId`, and StopTransaction by
     `transactionId` (it has no connectorId).
  3. All `charger.*` events carry `connectorId`; `command.remote.start` targets a connector (already has
     `ConnectorId`).
  4. Authorize (1.6) has no connectorId. Bind the VID to the connector of the next `Preparing`, then confirm on
     StartTransaction (see CB-008 #7, CB-010).
  5. Every connector has **its own meter** (different registers): MeterStartWh and energy per connector.
  6. Shared power: `Power.Offered` per connector moves as the other connector starts (50 → 20 + 30 kW).
     Show the offered power per connector; don't treat a drop as a fault.
  7. Onboarding: read `NumberOfConnectors` (GetConfiguration) and create the connectors from it. Keep support
     for per-id stations only if a real charger needs it.
  8. Station-level faults (`connectorId 0`, e.g. power-module codes) affect both connectors.
- **PR-011 checklist answers (from this unit):**
  - OCPP 1.6 only (no 2.0.1).
  - Model `DCF1-211-84600000`.
  - ISO 15118 vehicle id works as Autocharge `VID:<EVCCID>` with a Tesla. No PnC contract is needed for VID.
  - SoC/Voltage/Current/Power come in MeterValues and are parsed by `MeterValueParser` as is.
  - Phantom first-sample power spikes (117.5 / 161.7 kW on a 60 kW unit) → clamp to the station rating.
- **Reference here:** `docs/hardware/supernova-669850.md` (two-car table, per-connector meters).
  **Reference implementation here (2026-11-06):** EVOCPP `9b65bde` — `ConnectorSession` per connector,
  `PlugConnectors` table, connectorId in events, `MultiConnectorFlowTests` replaying the Supernova log;
  EVHomeAPI `e020c78` — `StationConnector`, open session per (station, connector), resume per connector;
  EVHomeApp `4355d70` — connector picker. Open: house limit on multi-connector chargers needs a
  station-wide `ChargePointMaxProfile` (`TxDefaultProfile` applies per connector).
- **Found:** 2026-11-05/06.

## CB-012 · Reconnect race: a superseded socket takes a live station offline
- **Target:** `OCPP_RD/OCPPServer/Program.cs` (WebSocket `finally` → `ChargingStationConnections.Remove(stationId)`,
  line ~223), `ChargingStationConnections.Add`
- **Status:** applied — OCPP_RD `9744209` (2026-11-06) + `c319e2c` `RemoveIfSocket` (2026-11-07)
- **Why:** a charger that opens a new WebSocket before the old one closed (seen on Wallbox Supernova, 2026-11-06) is
  registered under the new socket, then the old socket's `finally` removes the entry by id and publishes Offline.
  Result: the charger keeps sending (MeterValues arrive) but every command is discarded as "not connected" — RemoteStop
  of a running session failed twice — and the backend pauses / closes sessions on a false Offline.
- **Applied part:** `ChargingStationConnections.Add()` now carries over all `ConnectorSession` objects (including
  `PendingStart`/`PendingStop` TCS) from the old `ConnectorState` to the new one on reconnect. This fixes the
  case where a RemoteStart-initiated session is lost because the charger reconnects before `StartTransaction` arrives.
  Specifically: Wallbox Supernova sometimes disconnects+reconnects within 6s of entering Charging state; the
  `PendingStart` TCS was silently dropped, causing the session to Timeout/Cancel even though the charger was charging.
- **Remaining:** the `finally` / socket-identity check (so a stale socket disconnect doesn't publish Offline and
  doesn't remove the new socket's state). Full fix mirrors EVOCPP `efbc0fe`.
- **Also fixed 2026-11-06 (not a separate CB):**
  - `EVChargingApi/StationsController.cs` — added `.OrderBy(c => c.Id)` to connector list; without it PostgreSQL
    returned connector id=23 (OCPP connectorId=2) first, so the second physical port appeared as the first in the app.
  - `EVChargingApi/RabbitMqConsumerService.cs` — `HandleAuthorizeRequestedAsync` returns "Accepted" for unknown EVID
    cars (safe because `AutoChargeEnabled=false`); "Rejected" caused the charger to refuse `StartTransaction`
    even after RemoteStart "Accepted".
  - `Ocpp__TransactionConfirmTimeoutSeconds` needs to be set to 120 in Pi docker-compose (default 60s is too
    short when the reconnect eats 33s of the window and isolation test takes 25–35s).
- **Reference implementation here:** EVOCPP `efbc0fe` (+ tests in `ChargingStationConnectionsTests`).
- **Found:** 2026-11-06.

## CB-013 · Commercial stack on the shared Pi has no memory limits (Server GC, untuned RabbitMQ)
- **Target:** `EVChargingApi/docker-compose.yml` (services `api`, `ocpp`, `rabbitmq`, `db`, `admin`, `download`)
- **Status:** applied — EVChargingApi `bc18bae` (2026-11-07)
- **Why:** the Pi has 1 GB RAM (zram swap only compresses it). On 2026-11-06 the Pi filled swap, iowait hit 80 % and
  it rebooted in a loop until other stacks were stopped. Measured RSS+swap: `EVChargingApi.dll` 129 MB,
  `OCPPServer.dll` 113 MB, RabbitMQ 83 MB — no `mem_limit`, no GC settings (ASP.NET defaults to **Server GC**:
  one heap per core, keeps memory), default RabbitMQ watermark. Nabla Home's tuned stack runs in ≈ 210 MB.
- **Fix (values proven on Nabla Home, `deploy/docker-compose.yml`):**
  - .NET services: `mem_limit: 160m`, `DOTNET_gcServer=0`, `DOTNET_GCConserveMemory=5`,
    `DOTNET_GCHeapHardLimit=0x6400000` (100 MB managed heap)
  - RabbitMQ: `mem_limit: 192m`, `RABBITMQ_SERVER_ADDITIONAL_ERL_ARGS: "+S 1:1 +A 4"`, config file
    `vm_memory_high_watermark.absolute = 110MiB` (the env var is rejected by RabbitMQ 3.13+)
  - Postgres: keep it shared, but small `shared_buffers` / `work_mem` / `max_connections` if it is only a sandbox
  - `admin` / `download` (nginx): `mem_limit: 32m`
- **Reference here:** `deploy/docker-compose.yml`.
- **Found:** 2026-11-06, Pi hang during the Supernova tests.

## CB-014 · OCPP_RD has no Reset / ChangeAvailability commands
- **Target:** `OCPP_RD/OCPPServer` (admin REST next to `/api/admin/plugs/{id}/config`, and/or RabbitMQ commands)
- **Status:** idea
- **Why:** on 2026-11-07 the Supernova's `ConnectionUrl` was set back to its owner (1up) through
  `POST /api/admin/plugs/{id}/config` → `RebootRequired`. Without a `Reset` command it stayed on our server until a
  reboot, and people at the parking could not charge. `ChangeAvailability` (Inoperative / Operative) is also missing:
  it is the clean way to take a station out of service (drivers kept trying a charger marked "not working").
- **Fix:** admin REST `POST /api/admin/plugs/{id}/reset {type: Soft|Hard}` and
  `POST /api/admin/plugs/{id}/availability {connectorId, type: Inoperative|Operative}` (OCPP 1.6 Core), or a generic
  whitelisted call like EVOCPP's `command.dev.call`.
- **Reference here:** EVOCPP `DevCalls` (`Reset` whitelisted; add `ChangeAvailability` there too).
- **Found:** 2026-11-07.
