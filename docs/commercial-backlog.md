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
