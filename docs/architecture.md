# Nabla Home — Architecture

## Decisions

| # | Decision | Why |
|---|---|---|
| D1 | Fully separate project from the commercial Nabla solution (own repo, DB, backend, app) | Home features must not touch billing/fiscal code paths; different release cycles and data responsibility |
| D2 | OCPP gateway is shared **code**, not shared **instance**: same `ocppserver` image, separate `ocpp-home` container | EVOCPP (OCPP_RD repo) contains no commercial logic; forking would duplicate every vendor/protocol fix |
| D3 | Isolation via own Postgres DB (`ocpp_home`) and own RabbitMQ vhost (`/home`) | Exchange/queue names stay the same; no routing changes in the gateway |
| D4 | A charger is connected to exactly one gateway (commercial or home) | Moving a charger = changing its OCPP URL; no dual membership |
| D5 | OCPP 1.6J first | Most common home wallboxes |
| D6 | Schedules / current limits are pushed to the charger (`TxDefaultProfile`), RFID via Local Auth List | Must keep working when home internet drops |
| D7 | HomeApi exposes the same SignalR event shapes as EVChargingApi | Reuse the mobile `SessionService` pattern |

## Topology

```
HomeApp ──REST/SignalR──► HomeApi ──RabbitMQ (/home)──► ocpp-home ──WS──► home wallbox
                             │                              │
                          DB nablahome                   DB ocpp_home
```

## Stage 1 scope

1. EVOCPP: configurable RabbitMQ VirtualHost + CORS origins (backward compatible)
2. `deploy/docker-compose.yml`
3. HomeApi skeleton: auth (JWT), stations, station access, claim-by-code
4. OCPP integration: event consumer, authorize, sessions without cost, start/stop, SignalR

Stage 2: current limit, charging schedule, availability lock, RFID cards, family members.
