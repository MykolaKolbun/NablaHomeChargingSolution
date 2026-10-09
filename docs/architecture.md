# Nabla Home — Architecture

## Decisions

| # | Decision | Why |
|---|---|---|
| D1 | Fully separate project from the commercial Nabla solution (own repo, DB, backend, gateway, app) | Home features must not touch billing/fiscal code paths; different release cycles and data responsibility |
| D2 | Commercial solution (`..\NablaPublicChargingSolution`) is a **read-only reference** only | No coupling: nothing is shared at runtime or build time; reused code is copied in |
| D3 | Own OCPP gateway `EVOCPP/`, imported from commercial `OCPP_RD` at `8130e2e` with full history (git subtree), evolves independently | Home needs (smart charging, local auth list, simpler model) can diverge freely; no sync obligations either way |
| D4 | Own infrastructure: Postgres DBs `nablahome` + `evocpp`, own RabbitMQ (vhost `/home`), own images | Full isolation from commercial production |
| D10 | EVOCPP pushes `ChangeConfiguration(MeterValueSampleInterval = Ocpp:MeterValueSampleIntervalSeconds, default 60)` after every BootNotification | Wallbox Copper SB samples every ~300 s by default — too rare for the live screen; the setting persists on the charger, so it works without the backend |
| D11 | The charger always carries a limit profile ≤ `MaxCurrentA` (installation maximum): "no limit" = MaxCurrentA, pushed at claim and on every change; EVHomeAPI never sends ClearChargingProfile | Copper SB default 32 A exceeds the owner's 16 A house supply. The charger's hardware rotary switch / myWallbox max current stays the primary protection |
| D12 | A power loss pauses the session instead of ending it: charger offline or `StopTransaction(PowerLoss)` → `Paused`; when the charger is back and the car is still plugged, EVHomeAPI sends `RemoteStart` and the new transaction joins the same session (energy carried). Only RemoteStart — no CP-state manipulation. Also on every boot (`charger.booted`) the limit profile is re-pushed | Observed 2026-10-09 on Copper SB: after the outage it reports Finishing + `StopTransaction(PowerLoss)` and never resumes by itself, but accepts RemoteStart from Finishing. A reboot may wipe charging profiles |
| D9 | Hosted on the shared demo Pi as a lean stack with per-container `mem_limit`; built and deployed by GitHub Actions (same scheme as commercial) | Pi has ~1 GB RAM; limits make Nabla Home the OOM victim, never other projects. See `docs/deployment.md` |
| D5 | A charger is connected to exactly one system (commercial or home) | Moving a charger = changing its OCPP URL; no dual membership |
| D6 | OCPP 1.6J first | Most common home wallboxes |
| D7 | Schedules / current limits are pushed to the charger (`TxDefaultProfile`), RFID via Local Auth List | Must keep working when home internet drops |
| D8 | EVHomeAPI exposes the same SignalR event shapes as EVChargingApi | Reuse the mobile `SessionService` pattern (copied) |

## Topology

```
EVHomeApp ──REST/SignalR──► EVHomeAPI ──RabbitMQ (/home)──► EVOCPP ──WS──► home wallbox
                                │                              │
                           DB nablahome                    DB evocpp
```

## Stage 1 scope

1. ✅ EVOCPP imported; configurable RabbitMQ VirtualHost + CORS origins
2. ✅ `deploy/docker-compose.yml` + `.github/workflows/evocpp.yml` — deployed to the Pi 2026-10-08, `wss://home-ocpp.alternatiview.com.ua` answers (WebSocket 101)
3. ✅ (2026-10-08) Move DIY station `30011` to `home-ocpp.alternatiview.com.ua` — BootNotification, StatusNotification, Heartbeat OK
   - DIY hardware development is paused; the DIY station stays connected as-is.
   - End-to-end tests (RemoteStart → MeterValues → Stop, charging profiles) run on the **Wallbox Copper SB** test charger, powered on when needed.
4. EVOCPP: `SetChargingProfile` / `ClearChargingProfile` (current limit 6–16 A, TxDefault/Tx) — main feature of the DIY controller
5. ✅ (2026-10-08) EVHomeAPI skeleton: auth (JWT), stations, station access, claim-by-code — deployed, see `docs/api.md`
6. ✅ (2026-10-08) OCPP integration: event consumer, authorize, sessions without cost, start/stop, SignalR (incl. SoC from MeterValues) — deployed, 37 tests; e2e test on Wallbox Copper SB pending
7. EVHomeApp MVP: login/register, chargers, live charging screen, claim, history, profile — typecheck + web bundle OK, Login/Register checked in browser; device test pending

Stage 2:
- ✅ (2026-10-09) Resume after power loss (D12): session `Paused`, several transactions per session,
  automatic RemoteStart when the car is still plugged (3 attempts, 90 s apart; unplugged / failed /
  pause > 24 h → Completed with `stopReason: PowerLoss`, `endedAt` = start of the outage), limit
  re-push on boot. `SessionResumeService`, tests `PowerLossResumeTests`.
- Still open: escalation when the car refuses to restart (DIY: `DataTransfer CpReset` = IEC 61851 PWM
  pause B1→B2, never state A — opt-in, manual test first), push notification to the user; design in
  [diy-charger-controller.md](diy-charger-controller.md#відновлення-заряду-після-відключення-світла).
- Charging schedule, availability lock, RFID cards, family members.

Stage 3: energy management — read Deye inverter (Solarman V5 / Modbus), modes "solar only", "solar + minimum", "fast", "night tariff". Fast load balancing stays on the charger (ESP) and works offline.

Hardware context for the DIY charger: [diy-charger-controller.md](diy-charger-controller.md).
