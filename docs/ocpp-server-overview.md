# OCPPServer — Developer Overview

## What it is

OCPPServer is an ASP.NET Core (.NET 10) gateway that bridges physical EV charging stations to the rest of the platform. Chargers connect over WebSocket using the OCPP protocol; the server translates those messages into RabbitMQ events that the rest of the system consumes, and accepts RabbitMQ commands to send back to chargers.

Supported OCPP versions: **1.6J · 2.0 · 2.0.1 · 2.1**

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         OCPPServer                              │
│                                                                 │
│  WebSocket /ws/{stationId}                                      │
│  ┌──────────┐    ┌────────────┐    ┌────────────────────────┐  │
│  │ Charger  │───▶│  OcppRouter│───▶│ Ocpp16Communicator     │  │
│  │ OCPP 1.6 │    │            │    │ (OCPP 1.6J handler)    │  │
│  └──────────┘    │  picks     │    └────────────────────────┘  │
│                  │  handler   │                                  │
│  ┌──────────┐    │  based on  │    ┌────────────────────────┐  │
│  │ Charger  │───▶│  negotiated│───▶│ Ocpp21Communicator     │  │
│  │ OCPP 2.x │    │  protocol  │    │ (OCPP 2.0/2.0.1/2.1)  │  │
│  └──────────┘    └────────────┘    └────────────────────────┘  │
│                        │                      │                  │
│               ┌────────▼──────────────────────▼───────┐        │
│               │         RabbitMqPublisher              │        │
│               │         exchange: ocpp.events          │        │
│               └───────────────────────────────────────┘        │
│                                                                 │
│  ┌──────────────────────────────┐  ┌──────────────────────┐   │
│  │   RabbitMqConsumer           │  │   REST API           │   │
│  │   exchange: ocpp.commands    │  │   /api/admin/...     │   │
│  │   queue:  ocpp.server.cmds   │  └──────────────────────┘   │
│  │   → OcppCommandHandler       │                              │
│  └──────────────────────────────┘                              │
│                                                                 │
│  ┌──────────────────────────────┐  ┌──────────────────────┐   │
│  │  ChargingStationConnections  │  │   PostgreSQL DB      │   │
│  │  (in-memory connection map)  │  │   (Plugs table)      │   │
│  └──────────────────────────────┘  └──────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

---

## Protocol negotiation

At the WebSocket handshake the charger sends a `Sec-WebSocket-Protocol` header listing the OCPP versions it supports. The server picks the highest version it can handle:

```
ocpp2.1  >  ocpp2.0.1  >  ocpp2.0  >  ocpp1.6
```

The selected protocol is:
1. Returned to the charger in the `Sec-WebSocket-Protocol` response header.
2. Stored in `ChargingStationConnections` keyed by `stationId`.
3. Persisted to the `Plugs.OcppVersion` column on first `BootNotification`.

If a charger sends no `Sec-WebSocket-Protocol` header the server defaults to `ocpp1.6`.

---

## Key source files

| File | Purpose |
|---|---|
| `Program.cs` | Entry point — DI wiring, WebSocket endpoint, REST API endpoints, startup sequence |
| `OcppRouter.cs` | `ICommunicator` implementation — reads negotiated protocol and dispatches to the right handler |
| `OCPP1.6 Models/Communicator.cs` | Handles all OCPP 1.6J incoming messages and outbound commands |
| `OCPP2.1 Models/Ocpp21Communicator.cs` | Handles OCPP 2.0/2.0.1/2.1 incoming messages and outbound commands |
| `ChargingStationConnections.cs` | Thread-safe in-memory registry: WebSocket handles, negotiated protocols, pending transaction signals, OCPP 2.x transaction ID mapping |
| `RabbitMqPublisher.cs` | Publishes charger lifecycle events to `ocpp.events` exchange |
| `RabbitMQ Consumer.cs` | Consumes remote commands from `ocpp.commands` exchange |
| `OcppCommandHandler.cs` | Executes a command (remote start/stop, trigger) and publishes the result |
| `Data/ChargingDBContext.cs` | EF Core DbContext — single `Plugs` DbSet |
| `DataBase/DBModels/Plug.cs` | DB model for a charging station |
| `OcppTrace.cs` | Rolling-file trace logger with three levels |

---

## OCPP message handling

### OCPP 1.6J

Incoming messages handled:

| Action | What happens |
|---|---|
| `BootNotification` | Upserts the `Plug` record; sets `OcppVersion`, `IsOnline = true` |
| `Heartbeat` | Updates `LastStatusUpdate`; returns current server time |
| `StatusNotification` | Updates `Plug.Status`; publishes `charger.status.changed` |
| `StartTransaction` | Assigns a local `transactionId`; publishes `charger.transaction.started`; signals any pending remote-start waiter |
| `StopTransaction` | Clears session state; publishes `charger.transaction.stopped`; signals any pending remote-stop waiter |
| `MeterValues` | Extracts `Energy.Active.Import.Register` (Wh); calculates instantaneous power; publishes `charger.meter.updated` |
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
| `BootNotification` | Payload uses `chargingStation` object (`vendorName`, `model`, `serialNumber`, `firmwareVersion`, `modem.iccid`) instead of flat 1.6 fields |
| `Heartbeat` | Same behavior as 1.6 |
| `StatusNotification` | Uses `connectorStatus` (2.x values: `Available`, `Occupied`, `Reserved`, `Unavailable`, `Faulted`) and `evseId` instead of `connectorId` |
| `TransactionEvent(Started)` | Replaces `StartTransaction`. Assigns a local int transaction ID; maps it to the OCPP 2.x string `transactionId`; publishes `charger.transaction.started` |
| `TransactionEvent(Updated)` | Replaces periodic `MeterValues`. Updates meter reading; publishes `charger.meter.updated` |
| `TransactionEvent(Ended)` | Replaces `StopTransaction`. Clears session state; publishes `charger.transaction.stopped` |
| `MeterValues` | Some 2.x chargers send standalone `MeterValues` in addition to `TransactionEvent` — handled identically to 1.6 |
| Any other action | Acknowledged with an empty `CALLRESULT` |

Outbound commands:

| Method | OCPP action |
|---|---|
| `SendStartCharging` | `RequestStartTransaction` |
| `SendStopCharging` | `RequestStopTransaction` (looks up the OCPP string `transactionId` from the local int mapping) |

> **OCPP 2.x transaction IDs** — OCPP 2.x chargers use arbitrary string transaction IDs. Internally the server maps these to a local monotonic integer so that the RabbitMQ event contract (`transactionId: int`) stays consistent for all consumers regardless of charger protocol.

---

## RabbitMQ

### Outbound events  `ocpp.events` exchange (topic, durable)

| Routing key | Trigger |
|---|---|
| `charger.status.changed` | `StatusNotification` received, or charger WebSocket closed |
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
| `command.commandreq` | Sends `TriggerMessage` for any supported OCPP message type |
| `command.statusreq` | Shorthand for `TriggerMessage(StatusNotification)` |

---

## Database

Single PostgreSQL database, migrated automatically on startup via `db.Database.Migrate()`.

**`Plugs` table** — one row per physical charging station:

| Column | Type | Notes |
|---|---|---|
| `Id` | `int` | PK, identity |
| `OcppId` | `text` | Charger's OCPP identity (from WebSocket URL `/ws/{stationId}`) |
| `OcppVersion` | `text` | Negotiated protocol, e.g. `"ocpp1.6"` or `"ocpp2.1"` |
| `Status` | `int` | Last known `ChargePointStatus` enum value |
| `IsOnline` | `bool` | Updated by `BootNotification` and `Heartbeat` |
| `MeterValue` | `decimal?` | Last energy reading in Wh |
| `LastMeterValueAt` | `datetime?` | Timestamp of last meter reading — used for power calculation |
| `LastStatusUpdate` | `datetime?` | Timestamp of last change |
| `Vendor` | `text?` | From `BootNotification` |
| `ChargePointModel` | `text?` | From `BootNotification` |
| `ChargePointSN` | `text` | Serial number from `BootNotification` |
| `FirmwareVersion` | `text?` | From `BootNotification` |
| `SIMNr` | `text?` | ICCID from `BootNotification` |
| `IsFastCharger` | `bool` | Admin-set metadata |
| `MaxPower` | `int` | Admin-set max power in watts |
| `CreatedAt` | `datetime` | First seen |

---

## In-memory state (`ChargingStationConnections`)

The in-memory registry never persists to disk. On process restart:
- All WebSocket connections are gone (chargers reconnect and re-send `BootNotification`).
- Pending transaction signals are cleared (remote start/stop commands time out).
- `ActiveTransactionId` for resume-after-restart is read from DB if needed.

| Dictionary | Key | Value |
|---|---|---|
| `_connections` | `stationId` | Open `WebSocket` |
| `_protocols` | `stationId` | Negotiated protocol string |
| `_ocpp21Tx` | `stationId` | `(localIntId, ocppStringId)` — OCPP 2.x transaction mapping |
| `_meterStarts` | `stationId` | Wh reading at session start |
| `_pendingStartTransactions` | `stationId` | `TaskCompletionSource<int>` — completed when `StartTransaction` / `TransactionEvent(Started)` arrives |
| `_pendingStopTransactions` | `stationId` | `TaskCompletionSource<int>` — completed when `StopTransaction` / `TransactionEvent(Ended)` arrives |

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
5. ASP.NET Core begins accepting WebSocket connections and REST requests.

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
  }
}
```

---

## Key flows

### Charger connects and boots

```
Charger                          OCPPServer
  │── WebSocket upgrade ────────▶│  negotiate protocol
  │◀─ 101 Switching Protocols ───│  SetProtocol(stationId, "ocpp2.1")
  │── BootNotification ──────────▶│  upsert Plug; set OcppVersion
  │◀─ {"status":"Accepted"} ─────│
  │── StatusNotification ────────▶│  update Plug.Status
  │◀─ {} ────────────────────────│  publish charger.status.changed
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

```
Charger                          OCPPServer
  │── TCP close ────────────────▶│  ChargingStationConnections.Remove()
  │                               │  publish charger.status.changed
  │                               │    { status: "Offline", isConnected: false }
```
