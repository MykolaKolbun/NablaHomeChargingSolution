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
- **Status:** idea
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
- **Status:** idea
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
     `PMO:nnn:HWR:OF2` power-module output fuse, `PMO:nnn:HWR:OD2` output diode, `PMO:nnn:OVT:BAT`
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
- **Reference here:** `docs/hardware/supernova-669850.md` (all event timelines) and
  `docs/hardware/supernova-669850-config.json` (full GetConfiguration, 99 keys).
- **Found:** 2026-11-04 / 2026-11-05.
