# Nabla Home — Architecture

## Decisions

| # | Decision | Why |
|---|---|---|
| D1 | Fully separate project from the commercial Nabla solution (own repo, DB, backend, gateway, app) | Home features must not touch billing/fiscal code paths; different release cycles and data responsibility |
| D2 | Commercial solution (`..\NablaPublicChargingSolution`) is a **read-only reference** only | No coupling: nothing is shared at runtime or build time; reused code is copied in |
| D3 | Own OCPP gateway `EVOCPP/`, imported from commercial `OCPP_RD` at `8130e2e` with full history (git subtree), evolves independently | Home needs (smart charging, local auth list, simpler model) can diverge freely; no sync obligations either way |
| D4 | Own infrastructure: Postgres DBs `nablahome` + `evocpp`, own RabbitMQ (vhost `/home`), own images | Full isolation from commercial production |
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
5. EVHomeAPI skeleton: auth (JWT), stations, station access, claim-by-code
6. OCPP integration: event consumer, authorize (any idTag incl. `LOCAL` for owned stations), sessions without cost, start/stop, SignalR (incl. SoC from MeterValues)

Stage 2: charging schedule, availability lock, RFID cards, family members, Wallbox Copper SB onboarding.

Stage 3: energy management — read Deye inverter (Solarman V5 / Modbus), modes "solar only", "solar + minimum", "fast", "night tariff". Fast load balancing stays on the charger (ESP) and works offline.

Hardware context for the DIY charger: [diy-charger-controller.md](diy-charger-controller.md).
