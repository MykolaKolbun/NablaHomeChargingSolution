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
