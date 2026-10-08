# Nabla Home — Architecture

## Decisions

| # | Decision | Why |
|---|---|---|
| D1 | Fully separate project from the commercial Nabla solution (own repo, DB, backend, gateway, app) | Home features must not touch billing/fiscal code paths; different release cycles and data responsibility |
| D2 | Commercial solution (`..\NablaPublicChargingSolution`) is a **read-only reference** only | No coupling: nothing is shared at runtime or build time; reused code is copied in |
| D3 | Own OCPP gateway `EVOCPP/`, imported from commercial `OCPP_RD` at `8130e2e` with full history (git subtree), evolves independently | Home needs (smart charging, local auth list, simpler model) can diverge freely; no sync obligations either way |
| D4 | Own infrastructure: Postgres DBs `nablahome` + `evocpp`, own RabbitMQ (vhost `/home`), own images | Full isolation from commercial production |
| D5 | A charger is connected to exactly one system (commercial or home) | Moving a charger = changing its OCPP URL; no dual membership |
| D6 | OCPP 1.6J first | Most common home wallboxes |
| D7 | Schedules / current limits are pushed to the charger (`TxDefaultProfile`), RFID via Local Auth List | Must keep working when home internet drops |
| D8 | HomeApi exposes the same SignalR event shapes as EVChargingApi | Reuse the mobile `SessionService` pattern (copied) |

## Topology

```
HomeApp ──REST/SignalR──► HomeApi ──RabbitMQ (/home)──► EVOCPP ──WS──► home wallbox
                             │                             │
                          DB nablahome                  DB evocpp
```

## Stage 1 scope

1. ✅ EVOCPP imported; configurable RabbitMQ VirtualHost + CORS origins
2. `deploy/docker-compose.yml` (EVOCPP built from this repo)
3. HomeApi skeleton: auth (JWT), stations, station access, claim-by-code
4. OCPP integration: event consumer, authorize, sessions without cost, start/stop, SignalR

Stage 2: current limit, charging schedule, availability lock, RFID cards, family members.
