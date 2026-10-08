# OCPPServer — Developer Overview

## What it is

OCPPServer is an ASP.NET Core (.NET 10) gateway that bridges physical EV charging stations to the rest of the platform.
Chargers connect over WebSocket using the OCPP protocol;
the server translates those messages into RabbitMQ events that the rest of the system consumes, and accepts RabbitMQ commands to send back to chargers.

Supported OCPP versions: **1.6J · 2.0 · 2.0.1 · 2.1**

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                           OCPPServer                                │
│                                                                     │
│  WebSocket /ws/{stationId}                                          │
│  ┌──────────┐    ┌────────────┐    ┌────────────────────────────┐   │
│  │ Charger  │───▶│  OcppRouter│───▶│ Communicator               │   │
│  │ OCPP 1.6 │    │            │    │ (OCPP 1.6J handler)        │   │
│  └──────────┘    │  picks     │    └────────────────────────────┘   │
│                  │  handler   │                                     │
│  ┌──────────┐    │  based on  │    ┌────────────────────────────┐   │
│  │ Charger  │───▶│  negotiated│───▶│ Ocpp21Communicator         │   │
│  │ OCPP 2.x │    │  protocol  │    │ (OCPP 2.0/2.0.1/2.1)       │   │
│  └──────────┘    └────────────┘    └────────────────────────────┘   │
│                        │                        │                   │
│               ┌─────────▼────────────────────────▼──────┐           │
│               │           RabbitMqPublisher              │           │
│               │           exchange: ocpp.events          │           │
│               └──────────────────────────────────────────┘           │
│                                                                     │
│  ┌──────────────────────────────┐  ┌──────────────────────────┐     │
│  │   RabbitMqConsumer           │  │   REST API               │     │
│  │   exchange: ocpp.commands    │  │   /api/admin/...         │     │
│  │   queue:  ocpp.server.cmds   │  └──────────────────────────┘     │
│  │   → OcppCommandHandler       │                                   │
│  └──────────────────────────────┘                                   │
│                                                                     │
│  ┌──────────────────────────────┐                                   │
│  │   ChargerWatcherService      │  BackgroundService                │
│  │   scans every 30 s           │  aborts silent sockets            │
│  └──────────────────────────────┘                                   │
│                                                                     │
│  ┌────────────────────────────────────┐  ┌──────────────────────┐   │
│  │  ChargingStationConnections        │  │   PostgreSQL DB      │   │
│  │  ConcurrentDictionary              │  │   (Plugs table)      │   │
│  │  <stationId, ConnectorState>       │  └──────────────────────┘   │
│  └────────────────────────────────────┘                             │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Protocol negotiation

At the WebSocket handshake the charger sends a `Sec-WebSocket-Protocol` header listing the OCPP versions it supports. The server picks the highest version it can handle:

```
ocpp2.1  >  ocpp2.0.1  >  ocpp2.0  >  ocpp1.6
```

The selected protocol is:
1. Returned to the charger in the `Sec-WebSocket-Protocol` response header.
2. Stored in `ConnectorState.Protocol` keyed by `stationId`.
3. Persisted to `Plugs.OcppVersion` on first `BootNotification`.

If a charger sends no `Sec-WebSocket-Protocol` header the server defaults to `ocpp1.6`.

---

## Key source files

| File | Purpose |
|---|---|
| `Program.cs` | Entry point — DI wiring, WebSocket endpoint, REST API endpoints, startup sequence |
| `OcppRouter.cs` | `ICommunicator` — reads `ConnectorState.Protocol` and dispatches to the right handler |
| `OCPP1.6 Models/Communicator.cs` | Handles all OCPP 1.6J incoming messages and outbound commands |
| `OCPP2.1 Models/Ocpp21Communicator.cs` | Handles OCPP 2.0/2.0.1/2.1 incoming messages and outbound commands |
| `ConnectorState.cs` | In-memory live state per connected charger (socket, protocol, meter data, session IDs, pending signals) |
| `ChargingStationConnections.cs` | Thread-safe registry: `ConcurrentDictionary<string, ConnectorState>`; all station lookup/mutation goes through here |
| `ChargerAdapters/IChargerAdapter.cs` | Strategy interface for vendor-specific behaviour |
| `ChargerAdapters/DefaultChargerAdapter.cs` | Generic OCPP charger — delegates to `MeterValueParser` |
| `ChargerAdapters/WallboxChargerAdapter.cs` | Wallbox-specific — reads proprietary `meterType` from BootNotification |
| `ChargerAdapters/ChargerAdapterFactory.cs` | Creates the right adapter from the vendor name in BootNotification |
| `MeterValueParser.cs` | Shared meter value parser — handles OCPP 1.6 string units and OCPP 2.x object units; extracts Energy, SoC, Power.Active.Import |
| `RabbitMqPublisher.cs` | Publishes charger lifecycle events to `ocpp.events` exchange |
| `RabbitMQ Consumer.cs` | Consumes remote commands from `ocpp.commands` exchange |
| `OcppCommandHandler.cs` | Executes commands (remote start/stop, authorize response, trigger) and publishes results |
| `Data/ChargingDBContext.cs` | EF Core DbContext — single `Plugs` DbSet |
| `DataBase/DBModels/Plug.cs` | DB model for stable charger metadata |
| `ChargerWatcherService.cs` | `BackgroundService` — detects silently dead connections; marks `Plug.IsOnline = false`, aborts socket |
| `OcppTrace.cs` | Rolling-file trace logger with three levels |

---

## OCPP message handling

### OCPP 1.6J

Incoming messages handled:

| Action | What happens |
|---|---|
| `BootNotification` | Upserts `Plug` record; sets `OcppVersion`, `IsOnline = true`; creates vendor-specific `IChargerAdapter` and stores in `ConnectorState` |
| `Heartbeat` | Updates `Plug.LastStatusUpdate`; returns current server time |
| `Authorize` | If `idTag` starts with `"VID:"` (EVCC ID): publishes `charger.authorize.requested`, awaits `command.authorize.response` (10 s timeout → Rejected). Regular RFID/app tokens are accepted immediately. |
| `StatusNotification` | Updates `Plug.Status`; publishes `charger.status.changed` (includes `carId` on `Preparing`) |
| `StartTransaction` | Assigns local `transactionId`; stores meter start and session ID in `ConnectorState`; extracts `carId` if present; publishes `charger.transaction.started`; signals pending remote-start waiter |
| `StopTransaction` | Clears session state in `ConnectorState`; publishes `charger.transaction.stopped`; signals pending remote-stop waiter |
| `MeterValues` | Uses `ConnectorState.Adapter.ParseMeterValues()`; updates meter state in-memory only (no DB write); publishes `charger.meter.updated` |
| `DiagnosticsStatusNotification` | Publishes `charger.diagnostics.status` |

Outbound commands sent to chargers:

| Method | OCPP action |
|---|---|
| `SendStartCharging` | `RemoteStartTransaction` |
| `SendStopCharging` | `RemoteStopTransaction` |
| `SendTriggerMessage` | `TriggerMessage` |
| `SendGetDiagnostics` | `GetDiagnostics` |

### OCPP 2.0 / 2.0.1 / 2.1

Incoming messages handled:

| Action | Notes |
|---|---|
| `BootNotification` | Payload uses `chargingStation` object (`vendorName`, `model`, `serialNumber`, `firmwareVersion`, `modem.iccid`); creates vendor adapter from `vendorName` |
| `Heartbeat` | Same behavior as 1.6 |
| `Authorize` | If `idToken.type == "MacAddress"` or `additionalInfo[type=MacAddress]` present: publishes `charger.authorize.requested`, awaits response (10 s timeout → Rejected). Other token types accepted immediately. |
| `StatusNotification` | Uses `connectorStatus` field; includes `carId` on `Occupied` (2.x equivalent of `Preparing`) |
| `TransactionEvent(Started)` | Replaces `StartTransaction`; maps OCPP 2.x string `transactionId` to local int; extracts `carId` from `idToken` if not already set by `Authorize`; no DB write |
| `TransactionEvent(Updated)` | Replaces periodic `MeterValues`; all state written to `ConnectorState` only |
| `TransactionEvent(Ended)` | Replaces `StopTransaction`; clears session state |
| `MeterValues` | Standalone meter values (some 2.x chargers send both); handled identically to `TransactionEvent(Updated)` |
| Any other action | Acknowledged with an empty `CALLRESULT` |

Outbound commands:

| Method | OCPP action |
|---|---|
| `SendStartCharging` | `RequestStartTransaction` |
| `SendStopCharging` | `RequestStopTransaction` (looks up the OCPP string `transactionId` from `ConnectorState.OcppTxId`) |

> **OCPP 2.x transaction IDs** — chargers use arbitrary string IDs. The server maps them to local monotonic integers so all RabbitMQ events use a consistent `transactionId: int`.

---

## Vendor adapter pattern

Each connected charger gets an `IChargerAdapter` instance stored in its `ConnectorState`. The adapter is created (or replaced) on every `BootNotification` using `ChargerAdapterFactory.Create(vendor)`.

```
IChargerAdapter
├── DefaultChargerAdapter     ← generic OCPP (all vendors)
└── WallboxChargerAdapter     ← "Wall Box Chargers" vendor string
```

The adapter is responsible for:

| Method | Purpose |
|---|---|
| `OnBootNotification(payload)` | Read vendor-specific BootNotification fields (e.g. Wallbox's proprietary `meterType` key: `"Internal NON compliant"` vs `"External MID"`) |
| `ParseMeterValues(meterValues)` | Vendor-specific extraction of energy, SoC, power from a `meterValue` JArray |
| `MeterType` | Human-readable meter description exposed in the REST API |

**Wallbox External MID meter:** Wallbox chargers with an external certified MID meter report `meterType = "External MID"` in BootNotification. The meter is upstream of the charger and measures the full circuit including the charger's own self-consumption. The OCPP measurand name (`Energy.Active.Import.Register`) is the same; the physical source differs. The `WallboxChargerAdapter` stores and exposes this so the rest of the platform can handle billing correctly.

---

## RabbitMQ

### Outbound events  `ocpp.events` exchange (topic, durable)

| Routing key | Trigger |
|---|---|
| `charger.status.changed` | `StatusNotification` received, or charger WebSocket closed |
| `charger.authorize.requested` | `Authorize.req` received with EVCC ID — server awaiting response |
| `charger.transaction.started` | `StartTransaction` / `TransactionEvent(Started)` |
| `charger.transaction.stopped` | `StopTransaction` / `TransactionEvent(Ended)` |
| `charger.meter.updated` | `MeterValues` / `TransactionEvent(Updated)` |
| `charger.remote.start.response` | After `RemoteStartTransaction` / `RequestStartTransaction` |
| `charger.remote.stop.response` | After `RemoteStopTransaction` / `RequestStopTransaction` |
| `charger.trigger.response` | After `TriggerMessage` |
| `charger.diagnostics.status` | `DiagnosticsStatusNotification` |

See [messaging-api-spec.md](messaging-api-spec.md) for full payload schemas.

### Inbound commands  `ocpp.commands` exchange (topic, durable)

Queue `ocpp.server.commands`, binding `command.#`, prefetch = 1.

| Routing key | Effect |
|---|---|
| `command.remote.start` | Sends start command; waits up to 60 s for session confirmation |
| `command.remote.stop` | Sends stop command; waits up to 60 s for session end |
| `command.authorize.response` | Completes a pending `Authorize.req` — must arrive within 10 s |
| `command.commandreq` | Sends `TriggerMessage` for any supported OCPP message type |
| `command.statusreq` | Shorthand for `TriggerMessage(StatusNotification)` |

---

## Database

Single PostgreSQL database, migrated automatically on startup via `db.Database.Migrate()`.

**`Plugs` table** — one row per physical charging station (stable metadata only):

| Column | Type | Notes |
|---|---|---|
| `Id` | `int` | PK, identity |
| `OcppId` | `text` | Charger's OCPP identity (from WebSocket URL `/ws/{stationId}`) |
| `OcppVersion` | `text` | Negotiated protocol, e.g. `"ocpp1.6"` or `"ocpp2.1"` |
| `Status` | `int` | Last known `ChargePointStatus` enum value |
| `IsOnline` | `bool` | Updated by `BootNotification` and `Heartbeat` |
| `LastStatusUpdate` | `datetime?` | Timestamp of last DB write |
| `Vendor` | `text?` | From `BootNotification` |
| `ChargePointModel` | `text?` | From `BootNotification` |
| `ChargePointSN` | `text` | Serial number from `BootNotification` |
| `FirmwareVersion` | `text?` | From `BootNotification` |
| `SIMNr` | `text?` | ICCID from `BootNotification` |
| `IsFastCharger` | `bool` | Admin-set metadata |
| `MaxPower` | `int` | Admin-set max power in kW |
| `CreatedAt` | `datetime` | First seen |

> **Live meter data (`MeterValueWh`, `StateOfCharge`, `CurrentPowerKw`, etc.) is not persisted to the DB.** It is held in `ConnectorState` and resets on server restart. This eliminates a DB write on every meter update.

---

## In-memory state (`ConnectorState`)

One `ConnectorState` object per connected charger, stored in `ChargingStationConnections` (`ConcurrentDictionary<string, ConnectorState>`). Created on WebSocket connect, removed on disconnect.

| Field | Type | Notes |
|---|---|---|
| `Socket` | `WebSocket` | The open WebSocket connection |
| `Protocol` | `string` | Negotiated OCPP protocol, e.g. `"ocpp2.1"` |
| `LastMessageAt` | `DateTime` | UTC timestamp of the last received OCPP message; updated on every incoming message; used by `ChargerWatcherService` to detect silent drops |
| `Adapter` | `IChargerAdapter` | Vendor-specific behaviour strategy; set/replaced on BootNotification |
| `MeterValueWh` | `decimal?` | Latest energy reading (Wh) |
| `MeterStartWh` | `decimal?` | Session start reading (Wh); cleared on session end |
| `StateOfCharge` | `decimal?` | Battery SoC %; DC fast chargers only |
| `CurrentPowerKw` | `double?` | Latest power reading |
| `LastMeterValueAt` | `datetime?` | Timestamp of last meter update — used for ΔWh/Δh power calculation |
| `LocalTxId` | `int?` | Active local transaction ID |
| `OcppTxId` | `string?` | Active OCPP 2.x string transaction ID; `null` for 1.6 |
| `CarId` | `string?` | EVCC ID (vehicle MAC) if identified via ISO 15118; cleared on `Available` |
| `PendingStart` | `TCS<int>?` | Completed by `StartTransaction` / `TransactionEvent(Started)` |
| `PendingStop` | `TCS<int>?` | Completed by `StopTransaction` / `TransactionEvent(Ended)` |
| `PendingAuthorize` | `TCS<string>?` | Completed by `command.authorize.response`; holds `"Accepted"` or `"Rejected"` |

On process restart all in-memory state is lost. Chargers reconnect and re-send `BootNotification`, which re-creates their `ConnectorState`.

---

## Trace logging

Three-level rolling-file logger writing to `Trace:Directory` (default `/app/traces`):

| Level | Config value | Content |
|---|---|---|
| ERR | `0` | Errors and unhandled exceptions only |
| MSG | `1` | All OCPP and RabbitMQ messages in/out (default) |
| DBG | `2` | Method-level detail + all messages |

Files rotate at `Trace:MaxFileMb` (default 10 MB): `OCPP_Trace1.txt` (current) → `OCPP_Trace2.txt` → `OCPP_Trace3.txt`. Downloadable via REST at `GET /api/admin/traces/{filename}?key=<Trace:DownloadKey>`.

Log line format:
```
22.05.2026 14:35:22 MSG  OCPP: [← CALL] u030: action=Heartbeat payload={}
22.05.2026 14:35:22 MSG  OCPP: [→ OUT] [3,"abc123",{"currentTime":"..."}]
22.05.2026 14:35:22 MSG  RMQ : [→] charger.status.changed
```

---

## Startup sequence

1. EF migrations applied (`db.Database.Migrate()`).
2. `OcppTrace` configured from `appsettings.json`.
3. `RabbitMqPublisher.ConfigureAsync()` — connects with retry loop; server is not ready until this returns.
4. `RabbitMqConsumer` hosted service starts consuming `ocpp.server.commands`.
5. `ChargerWatcherService` hosted service starts the background scan loop.
6. ASP.NET Core begins accepting WebSocket connections and REST requests.

---

## Configuration reference

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=...;Database=charging;Username=...;Password=..."
  },
  "RabbitMQ": {
    "Host":     "rabbitmq",
    "Username": "guest",
    "Password": "guest"
  },
  "Trace": {
    "Directory":   "/app/traces",
    "Level":       "1",
    "MaxFileMb":   "10",
    "DownloadKey": "<secret>"
  },
  "ChargerWatcher": {
    "CheckIntervalSeconds":       30,
    "InactivityThresholdSeconds": 180
  }
}
```

---

## Key flows

### Charger connects and boots

```
Charger                          OCPPServer
  │── WebSocket upgrade ────────▶│  negotiate protocol
  │◀─ 101 Switching Protocols ───│  ConnectorState created { Protocol = "ocpp2.1" }
  │── BootNotification ──────────▶│  upsert Plug; set OcppVersion
  │                               │  Adapter = ChargerAdapterFactory.Create(vendor)
  │◀─ {"status":"Accepted"} ─────│
  │── StatusNotification ────────▶│  update Plug.Status
  │◀─ {} ────────────────────────│  publish charger.status.changed
```

### ISO 15118 auto-authorize (vehicle identity)

```
Charger                    OCPPServer                Backend
  │── Authorize.req ──────▶│                              │
  │   idTag:"VID:ABC"      │── charger.authorize.requested ──▶│
  │                        │   { ocppId, carId:"ABC" }    │
  │                        │          (await, 10 s max)   │
  │                        │◀── command.authorize.response ──│
  │◀─ Authorize.conf ──────│   { status:"Accepted" }      │
  │   { "Accepted" }       │                              │
  │── StartTransaction ───▶│                              │
  │◀─ response ────────────│── charger.transaction.started ──▶│
```

### Remote start (backend-initiated)

```
Backend                  OCPPServer              Charger
  │── command.remote.start ──▶│                     │
  │                           │── RemoteStart ──────▶│
  │                           │◀─ Accepted ──────────│
  │                           │                      │ (user plugs in)
  │                           │◀─ StartTransaction ──│
  │                           │── response ─────────▶│
  │◀── charger.transaction.started ──│               │
  │◀── charger.remote.start.response ──│             │
```

### Charger disconnects

**Clean disconnect:**
```
Charger                          OCPPServer
  │── TCP close ────────────────▶│  ChargingStationConnections.Remove()
  │                               │  ConnectorState destroyed
  │                               │  publish charger.status.changed
  │                               │    { status: "Offline", isConnected: false }
```

**Silent drop (watcher-detected):**
```
                                  OCPPServer               ChargerWatcherService
  [charger silently dead]          │                               │
                                   │◄── scan: LastMessageAt > 180s ─│
                                   │                               │
                                   │    DB: Plug.IsOnline = false  │
                                   │    socket.Abort()             │
                                   │                               │
                                   │  (Program.cs finally block)   │
                                   │  Remove() + PushDisconnect()  │
                                   │  → charger.status.changed     │
                                   │    { isConnected: false }     │
```
