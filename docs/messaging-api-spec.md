# OCPPServer — RabbitMQ & REST API Specification

**Version:** 1.0  
**Protocol:** OCPP 1.6J  
**Audience:** Backend services integrating with OCPPServer

---

## Table of Contents

1. [Overview](#1-overview)
2. [RabbitMQ Topology](#2-rabbitmq-topology)
3. [Outbound Events (OCPPServer → Consumers)](#3-outbound-events-ocppserver--consumers)
4. [Inbound Commands (Consumers → OCPPServer)](#4-inbound-commands-consumers--ocppserver)
5. [REST API](#5-rest-api)
6. [Configuration](#6-configuration)
7. [Sequence Flows](#7-sequence-flows)
8. [Error Handling](#8-error-handling)

---

## 1. Overview

OCPPServer is a stateful WebSocket gateway that speaks OCPP 1.6J to physical charging stations. It bridges charger events to your backend via RabbitMQ and exposes administrative actions via a REST API.

```
Charging Station ←──WebSocket──→ OCPPServer ←──RabbitMQ──→ Your Backend
                                      │
                                      └──── REST API ──→ Admin UI / CSMS
```

- **OCPPServer publishes** charger lifecycle events to a RabbitMQ topic exchange.
- **Your backend publishes** remote commands to a separate exchange; OCPPServer consumes and executes them.
- **REST API** covers administrative operations (plug metadata, diagnostics, trace downloads).

---

## 2. RabbitMQ Topology

### 2.1 Events Exchange (OCPPServer → Consumers)

| Property | Value |
|---|---|
| Exchange name | `ocpp.events` |
| Exchange type | `topic` |
| Durable | `true` |
| Auto-delete | `false` |

Consumers declare their own queues and bind with routing-key patterns (e.g., `charger.#` or `charger.transaction.*`).

### 2.2 Commands Exchange (Consumers → OCPPServer)

| Property | Value |
|---|---|
| Exchange name | `ocpp.commands` |
| Exchange type | `topic` |
| Durable | `true` |
| Queue name | `ocpp.server.commands` |
| Binding key | `command.#` |
| Consumer prefetch | 1 |
| Auto-ack | `false` (manual ack after processing) |

Publish commands directly to `ocpp.commands` with the appropriate routing key.

---

## 3. Outbound Events (OCPPServer → Consumers)

All messages are JSON, UTF-8, content-type `application/json`.

### 3.1 `charger.status.changed`

Emitted when a charger connects, disconnects, or reports a status change.

```json
{
  "ocppId":      "CP-001",
  "status":      "Available",
  "connectorId": 1,
  "isConnected": true
}
```

| Field | Type | Notes |
|---|---|---|
| `ocppId` | `string` | Station serial / OCPP identity |
| `status` | `string` | OCPP 1.6 `ChargePointStatus`: `Available`, `Preparing`, `Charging`, `SuspendedEVSE`, `SuspendedEV`, `Finishing`, `Reserved`, `Unavailable`, `Faulted` |
| `connectorId` | `int` | `0` = station-level, `1+` = individual connector |
| `isConnected` | `bool` | `true` = WebSocket is open, `false` = station has disconnected |

### 3.2 `charger.transaction.started`

Emitted when a charging session begins (OCPP `StartTransaction.req` received).

```json
{
  "ocppId":       "CP-001",
  "transactionId": 42,
  "meterStartWh": 12500.00
}
```

| Field | Type | Notes |
|---|---|---|
| `ocppId` | `string` | Station identity |
| `transactionId` | `int` | OCPP-assigned transaction ID |
| `meterStartWh` | `decimal?` | Energy meter reading at session start (Wh); `null` if not provided |

### 3.3 `charger.transaction.stopped`

Emitted when a charging session ends (OCPP `StopTransaction.req` received).

```json
{
  "ocppId":        "CP-001",
  "transactionId": 42,
  "meterStopWh":   18750.00,
  "meterStartWh":  12500.00
}
```

| Field | Type | Notes |
|---|---|---|
| `ocppId` | `string` | Station identity |
| `transactionId` | `int` | OCPP transaction ID |
| `meterStopWh` | `decimal` | Energy meter reading at session end (Wh) |
| `meterStartWh` | `decimal?` | Energy meter reading at session start (Wh); `null` if unavailable |

**Energy consumed** = `meterStopWh - meterStartWh`

### 3.4 `charger.meter.updated`

Emitted periodically during a session when the charger sends `MeterValues` notifications.

```json
{
  "ocppId":          "CP-001",
  "meterValueWh":    15320.50,
  "meterStartWh":    12500.00,
  "currentPowerKw":  7.4
}
```

| Field | Type | Notes |
|---|---|---|
| `ocppId` | `string` | Station identity |
| `meterValueWh` | `decimal` | Current cumulative energy (Wh) |
| `meterStartWh` | `decimal?` | Session start meter value; `null` if not yet known |
| `currentPowerKw` | `double?` | Active power in kW; `null` if not reported by charger |

### 3.5 `charger.remote.start.response`

Response to a `command.remote.start` command.

```json
{
  "ocppId":      "CP-001",
  "status":      "Accepted",
  "connectorId": 1,
  "idTag":       "RFID123456"
}
```

| Field | Type | Notes |
|---|---|---|
| `ocppId` | `string` | Station identity |
| `status` | `string` | `Accepted` — session started (StartTransaction received within 60 s); `Rejected` — station refused; `Timeout` — no StartTransaction within 60 s |
| `connectorId` | `int` | Connector the session started on |
| `idTag` | `string` | ID tag from the original command |

### 3.6 `charger.remote.stop.response`

Response to a `command.remote.stop` command.

```json
{
  "ocppId":        "CP-001",
  "status":        "Accepted",
  "transactionId": 42
}
```

| Field | Type | Notes |
|---|---|---|
| `ocppId` | `string` | Station identity |
| `status` | `string` | `Accepted` — session stopped (StopTransaction received within 60 s); `Rejected` — station refused; `Timeout` — no StopTransaction within 60 s |
| `transactionId` | `int?` | Transaction ID from the original command; `null` if not provided |

### 3.7 `charger.trigger.response`

Response to `command.commandreq` or `command.statusreq`.

```json
{
  "ocppId":           "CP-001",
  "status":           "Accepted",
  "requestedMessage": "StatusNotification"
}
```

| Field | Type | Notes |
|---|---|---|
| `ocppId` | `string` | Station identity |
| `status` | `string` | OCPP `TriggerMessageStatus`: `Accepted`, `Rejected`, `NotImplemented` |
| `requestedMessage` | `string` | The message type that was triggered |

### 3.8 `charger.diagnostics.status`

Emitted when a charger sends a `DiagnosticsStatusNotification` (after a `GetDiagnostics` request).

```json
{
  "ocppId": "CP-001",
  "status": "Uploaded"
}
```

| Field | Type | Notes |
|---|---|---|
| `ocppId` | `string` | Station identity |
| `status` | `string` | OCPP `DiagnosticsStatus`: `Idle`, `Uploaded`, `UploadFailed`, `Uploading` |

---

## 4. Inbound Commands (Consumers → OCPPServer)

Publish to exchange `ocpp.commands`. OCPPServer will process the command, execute the corresponding OCPP operation, and publish a response event to `ocpp.events`.

### 4.1 `command.remote.start`

Remotely start a charging session on a specific connector.

```json
{
  "OcppId":      "CP-001",
  "ConnectorId": 1,
  "IdTag":       "RFID123456"
}
```

| Field | Type | Required | Default | Notes |
|---|---|---|---|---|
| `OcppId` | `string` | yes | — | Station OCPP identity |
| `ConnectorId` | `int` | no | `1` | Target connector |
| `IdTag` | `string` | yes | — | Authorization tag to use |

**Flow:**
1. OCPPServer sends `RemoteStartTransaction` to the charger.
2. If the charger responds `Accepted`, OCPPServer waits up to **60 seconds** for `StartTransaction.req`.
3. Publishes `charger.remote.start.response` with `Accepted`, `Rejected`, or `Timeout`.

> **Note:** `Accepted` on the OCPP response only means the charger will *attempt* to start. Wait for `charger.transaction.started` to confirm the session is live.

### 4.2 `command.remote.stop`

Remotely stop an active charging session.

```json
{
  "OcppId":        "CP-001",
  "TransactionId": 42
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `OcppId` | `string` | yes | Station OCPP identity |
| `TransactionId` | `int?` | no | OCPP transaction ID; if omitted the charger may apply it to the active session |

**Flow:**
1. OCPPServer sends `RemoteStopTransaction` to the charger.
2. If the charger responds `Accepted`, OCPPServer waits up to **60 seconds** for `StopTransaction.req`.
3. Publishes `charger.remote.stop.response` with `Accepted`, `Rejected`, or `Timeout`.

### 4.3 `command.commandreq`

Trigger any OCPP 1.6 message from the charger via `TriggerMessage`.

```json
{
  "ocppId":           "CP-001",
  "requestedMessage": "MeterValues",
  "connectorId":      1
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `ocppId` | `string` | yes | Station OCPP identity |
| `requestedMessage` | `string` | yes | OCPP `MessageTrigger`: `BootNotification`, `DiagnosticsStatusNotification`, `FirmwareStatusNotification`, `Heartbeat`, `MeterValues`, `StatusNotification` |
| `connectorId` | `int?` | no | Scopes the trigger to a connector (relevant for `MeterValues`, `StatusNotification`) |

Publishes `charger.trigger.response` on completion.

### 4.4 `command.statusreq`

Shorthand for `TriggerMessage(StatusNotification)` — triggers a status report from the charger.

```json
{
  "ocppId":      "CP-001",
  "connectorId": 1
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `ocppId` | `string` | yes | Station OCPP identity |
| `connectorId` | `int?` | no | Scopes to a specific connector |

Publishes `charger.trigger.response` on completion.

---

## 5. REST API

Base URL depends on deployment. All admin endpoints require the request origin to be `https://admin.alternatiview.com.ua` (CORS policy).

### 5.1 List All Plugs

```
GET /api/admin/plugs
```

**Response 200:**

```json
[
  {
    "ocppId":       "CP-001",
    "isFastCharger": false,
    "maxPower":     7400,
    "isConnected":  true,
    "status":       "Charging",
    "connectorId":  1
  }
]
```

| Field | Type | Notes |
|---|---|---|
| `ocppId` | `string` | Station OCPP identity |
| `isFastCharger` | `bool` | Metadata flag |
| `maxPower` | `int` | Maximum power in watts |
| `isConnected` | `bool` | Real-time WebSocket connection state |
| `status` | `string` | Last known OCPP status |
| `connectorId` | `int` | Connector number |

### 5.2 Get Plug Detail

```
GET /api/admin/plugs/{ocppId}
```

**Path parameters:**

| Parameter | Type | Notes |
|---|---|---|
| `ocppId` | `string` | Station OCPP identity |

**Response 200:** Same object shape as a single item from the list above.

**Response 404:**
```json
{ "error": "Not found" }
```

### 5.3 Update Plug Metadata

```
PUT /api/admin/plugs/{ocppId}
Content-Type: application/json
```

**Request body:**

```json
{
  "isFastCharger": true,
  "maxPower":      22000
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `isFastCharger` | `bool` | yes | Marks station as fast charger |
| `maxPower` | `int` | yes | Maximum power in watts |

**Response 200:**
```json
{ "success": true }
```

**Response 404:**
```json
{ "error": "Not found" }
```

### 5.4 Request Diagnostics

```
POST /api/admin/plugs/{ocppId}/diagnostics
Content-Type: application/json
```

Sends a `GetDiagnostics` OCPP command to the charger. The charger will upload its diagnostic file to the configured FTP/SFTP location and send `DiagnosticsStatusNotification` updates (forwarded as `charger.diagnostics.status` events on RabbitMQ).

**Request body:**

```json
{
  "location":     "ftp://diagnostics.example.com/uploads/",
  "startTime":    "2026-05-01T00:00:00Z",
  "stopTime":     "2026-05-25T23:59:59Z",
  "retries":      3,
  "retryInterval": 60
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `location` | `string` | yes | Upload destination URI (FTP / SFTP) |
| `startTime` | `datetime` | no | Start of diagnostic time window (ISO 8601 UTC) |
| `stopTime` | `datetime` | no | End of diagnostic time window (ISO 8601 UTC) |
| `retries` | `int` | no | Number of upload retries |
| `retryInterval` | `int` | no | Retry interval in seconds |

**Response 200:**
```json
{
  "ocppId":   "CP-001",
  "fileName": "CP-001_diagnostics_20260525.log"
}
```

**Response 404:** Station not connected.

### 5.5 List Trace Files

```
GET /api/admin/traces?key=<secret>
```

Returns a list of rolling trace log files stored on the server.

**Query parameters:**

| Parameter | Type | Required | Notes |
|---|---|---|---|
| `key` | `string` | yes | Value of `Trace:DownloadKey` in server config |

**Response 200:**
```json
[
  {
    "name":        "trace-20260525.log",
    "sizeBytes":   204800,
    "downloadUrl": "/api/admin/traces/trace-20260525.log?key=<secret>"
  }
]
```

**Response 401:** Missing or incorrect `key`.

### 5.6 Download Trace File

```
GET /api/admin/traces/{filename}?key=<secret>
```

**Path parameters:**

| Parameter | Type | Notes |
|---|---|---|
| `filename` | `string` | File name from the list endpoint |

**Response 200:** `Content-Type: text/plain` — raw log file content.

**Response 401:** Missing or incorrect `key`.

**Response 404:** File not found.

---

## 6. Configuration

OCPPServer reads its configuration from `appsettings.json` (overridable by environment variables).

```json
{
  "RabbitMq": {
    "Host":     "rabbitmq.example.com",
    "Port":     5672,
    "Username": "ocpp",
    "Password": "secret",
    "VirtualHost": "/"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=pg.example.com;Database=charging;Username=ocpp;Password=secret"
  },
  "Trace": {
    "Directory":   "logs/traces",
    "DownloadKey": "<secret-key-for-trace-download>"
  }
}
```

| Key | Notes |
|---|---|
| `RabbitMq:Host` | RabbitMQ broker hostname |
| `RabbitMq:Port` | Default `5672` |
| `RabbitMq:Username` / `Password` | Broker credentials |
| `RabbitMq:VirtualHost` | Default `/` |
| `Trace:DownloadKey` | Shared secret for the `/api/admin/traces` endpoints |

---

## 7. Sequence Flows

### 7.1 Remote Start Session

```
Your Backend                    OCPPServer               Charger
     │                               │                      │
     │── command.remote.start ──────►│                      │
     │   {OcppId, ConnectorId,       │                      │
     │    IdTag}                     │                      │
     │                               │── RemoteStartTransaction ──►│
     │                               │◄── Accepted ───────────────│
     │                               │                             │
     │                               │◄── StartTransaction.req ───│
     │                               │── StartTransaction.conf ──►│
     │                               │                             │
     │◄── charger.transaction.started ──│                         │
     │◄── charger.remote.start.response (Accepted) ──│            │
     │    {status: "Accepted"}       │                             │
```

If charger responds `Rejected`:
```
     │◄── charger.remote.start.response (Rejected) ──│
```

If no `StartTransaction` arrives within 60 s:
```
     │◄── charger.remote.start.response (Timeout) ──│
```

### 7.2 Remote Stop Session

```
Your Backend                    OCPPServer               Charger
     │                               │                      │
     │── command.remote.stop ───────►│                      │
     │   {OcppId, TransactionId}     │                      │
     │                               │── RemoteStopTransaction ───►│
     │                               │◄── Accepted ───────────────│
     │                               │                             │
     │                               │◄── StopTransaction.req ────│
     │                               │── StopTransaction.conf ───►│
     │                               │                             │
     │◄── charger.transaction.stopped ──│                         │
     │◄── charger.remote.stop.response (Accepted) ──│             │
```

### 7.3 Organic Session (charger-initiated)

```
Your Backend                    OCPPServer               Charger
     │                               │◄── StartTransaction.req ───│
     │                               │── StartTransaction.conf ──►│
     │◄── charger.transaction.started ──│                         │
     │                               │                             │
     │                 (session active — meter updates)            │
     │                               │◄── MeterValues.req ────────│
     │◄── charger.meter.updated ────│                             │
     │                               │                             │
     │                               │◄── StopTransaction.req ────│
     │                               │── StopTransaction.conf ───►│
     │◄── charger.transaction.stopped ──│                         │
```

---

## 8. Error Handling

### RabbitMQ Commands

- OCPPServer uses **prefetch=1** and **manual ack**. If the server crashes before acking, the message is re-queued and retried on reconnect.
- If the target charger is **not connected**, the command is nacked (not re-queued) and no response event is emitted. Your backend should set a reasonable timeout when waiting for a response event.
- Command processing is **synchronous per connection** — OCPPServer waits for the charger response before processing the next command from the queue.

### REST API

- All 4xx/5xx responses include a JSON body `{ "error": "<description>" }`.
- The diagnostics endpoint returns `404` if the charger is not currently connected (cannot deliver the OCPP command).

### Connection Loss

- When a charger disconnects, OCPPServer emits `charger.status.changed` with `isConnected: false`.
- In-flight command waits (remote start/stop) will time out after 60 seconds and emit a `Timeout` response event.
