# OCPPServer — RabbitMQ & REST API Specification

**Version:** 1.7  
**Protocols:** OCPP 1.6J · OCPP 2.0 · OCPP 2.0.1 · OCPP 2.1  
**Audience:** Backend services integrating with OCPPServer

---

## Table of Contents

1. [Overview](#1-overview)
2. [RabbitMQ Topology](#2-rabbitmq-topology)
3. [Outbound Events (OCPPServer → Consumers)](#3-outbound-events-ocppserver--consumers)
4. [Inbound Commands (Consumers → OCPPServer)](#4-inbound-commands-consumers--ocppserver)
5. [REST API](#5-rest-api)
   - 5.1 List All Plugs
   - 5.2 Get Plug Detail
   - 5.3 Update Plug Metadata
   - 5.4 Request Diagnostics
   - 5.5 List Error Logs
   - 5.6 Mark Error Log as Solved
   - 5.7 List Trace Files
   - 5.8 Download Trace File
6. [Configuration](#6-configuration)
7. [Sequence Flows](#7-sequence-flows)
8. [Error Handling](#8-error-handling)

---

## 1. Overview

OCPPServer is a stateful WebSocket gateway that supports OCPP 1.6J, 2.0, 2.0.1, and 2.1. The protocol is negotiated at the WebSocket handshake via the `Sec-WebSocket-Protocol` header; the server always picks the highest version both sides support. From a consumer's perspective the RabbitMQ event and command contracts are identical regardless of which OCPP version the charger uses — differences are absorbed internally.

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
  "status":      "Preparing",
  "connectorId": 1,
  "isConnected": true,
  "carId":       "001681020001"
}
```

| Field | Type | Notes |
|---|---|---|
| `ocppId` | `string` | Station serial / OCPP identity |
| `status` | `string` | **Exact status string as reported by the charger — never translated by OCPPServer.** OCPP 1.6 values: `Available`, `Preparing`, `Charging`, `SuspendedEVSE`, `SuspendedEV`, `Finishing`, `Reserved`, `Unavailable`, `Faulted`. OCPP 2.x values: `Available`, `Occupied`, `Reserved`, `Unavailable`, `Faulted`. Interpretation of protocol differences (e.g. `Occupied` vs `Preparing`/`Charging`) is left to the consumer. |
| `connectorId` | `int` | `0` = station-level, `1+` = individual connector |
| `isConnected` | `bool` | `true` = WebSocket is open, `false` = station has disconnected |
| `carId` | `string?` | EVCC ID (vehicle MAC address) if the vehicle identified itself via ISO 15118 / DIN 70121. Present only on status `Preparing` (1.6) / `Occupied` (2.x) and only for DC fast chargers. `null` for all other statuses and for AC chargers. |

> **`carId` lifecycle:** set when the charger sends `Authorize.req` with a vehicle identity token (or from the first `TransactionEvent(Started)` that carries one); cleared when the connector returns to `Available`.

> **OCPP vocabulary note:** `status` is forwarded verbatim — OCPPServer does not translate between OCPP versions. In a mixed fleet, consumers receive `"Preparing"` from 1.6 chargers and `"Occupied"` from 2.x chargers for the same physical state. Map these on the consumer side to your own domain status model.

### 3.2 `charger.authorize.requested`

Emitted when a DC fast charger sends an `Authorize.req` carrying a vehicle identity (EVCC ID). **OCPPServer is waiting for your response** — you must publish `command.authorize.response` within **10 seconds** or the server will automatically reject the vehicle.

```json
// OCPP 2.x with evseId present
{ "ocppId": "CP-001", "carId": "001681020001", "connectorId": 1 }

// OCPP 1.6, or OCPP 2.x without evseId
{ "ocppId": "CP-001", "carId": "001681020001" }
```

| Field | Type | Notes |
|---|---|---|
| `ocppId` | `string` | Station identity |
| `carId` | `string` | EVCC ID (hex MAC address, no colons) — uniquely identifies the vehicle |
| `connectorId` | `int` | *(optional)* EVSE/connector number (1-based). Present only for OCPP 2.x chargers that include `evseId` in their `Authorize.req`. **Key is absent entirely** for OCPP 1.6 (which has no EVSE field in Authorize) and for 2.x chargers that omit `evseId`. |

**Required response:** publish `command.authorize.response` within 10 s (see §4.3).

> This event is only emitted for vehicle-identity tokens (`VID:` prefix in OCPP 1.6; `type=MacAddress` in OCPP 2.x). Regular RFID / app tokens are accepted immediately without publishing this event.

### 3.3 `charger.transaction.started`

Emitted when a charging session begins (`StartTransaction` on OCPP 1.6; `TransactionEvent(Started)` on OCPP 2.x).

```json
{
  "ocppId":        "CP-001",
  "transactionId": 42,
  "meterStartWh":  12500.00
}
```

| Field | Type | Notes |
|---|---|---|
| `ocppId` | `string` | Station identity |
| `transactionId` | `int` | Server-assigned transaction ID (OCPP 2.x string IDs are mapped to a local int) |
| `meterStartWh` | `decimal?` | Energy meter reading at session start (Wh); `null` if not provided |

### 3.4 `charger.transaction.stopped`

Emitted when a charging session ends (`StopTransaction` on OCPP 1.6; `TransactionEvent(Ended)` on OCPP 2.x).

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

### 3.5 `charger.meter.updated`

Emitted periodically during a session when the charger sends `MeterValues` notifications.

```json
{
  "ocppId":          "CP-001",
  "meterValueWh":    15320.50,
  "meterStartWh":    12500.00,
  "currentPowerKw":  7.4,
  "soc":             82.5
}
```

| Field | Type | Notes |
|---|---|---|
| `ocppId` | `string` | Station identity |
| `meterValueWh` | `decimal` | Current cumulative energy (Wh) |
| `meterStartWh` | `decimal?` | Session start meter value; `null` if not yet known |
| `currentPowerKw` | `double?` | Active power in kW; directly reported by charger (`Power.Active.Import`) or derived from consecutive energy readings; `null` if unavailable |
| `soc` | `decimal?` | State of Charge (%, 0–100); only populated by DC fast chargers that communicate with the vehicle BMS; `null` for AC chargers |

### 3.6 `charger.remote.start.response`

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

### 3.7 `charger.remote.stop.response`

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

### 3.8 `charger.trigger.response`

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

### 3.9 `charger.diagnostics.status`

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

### 3.10 `charger.charging.limit.response`

Result of `command.charging.limit` / `command.charging.clear` (§4.6, §4.7).

```json
{ "ocppId": "03012", "limitA": 16, "status": "Accepted", "txStatus": "Accepted" }
```

| Field | Type | Notes |
|---|---|---|
| `ocppId` | `string` | Station identity |
| `limitA` | `double?` | Requested limit; `null` for a clear |
| `status` | `string` | Result for the `TxDefaultProfile`: `Accepted`, `Rejected`, `NotSupported`, `Unknown` (clear: no such profile), `Timeout`, `Error` |
| `txStatus` | `string?` | Result for the running-transaction `TxProfile`; `null` when no `transactionId` was given (or the default profile failed) |

`NotSupported` is also returned without contacting the charger for OCPP 2.x stations (not implemented yet).

### 3.11 `charger.dev.call.response`

Result of `command.dev.call` (§4.8).

```json
{ "ocppId": "03012", "requestId": "7f1c…", "action": "GetConfiguration", "status": "Ok",
  "result": { "configurationKey": [ { "key": "MeterValueSampleInterval", "readonly": false, "value": "60" } ] } }
```

| Field | Notes |
|---|---|
| `requestId` | Echo of the command's `requestId` |
| `status` | `Ok` (charger answered), `Invalid` (action/payload rejected by the whitelist), `NotConnected`, `NotSupported` (OCPP 2.x), `Timeout`, `Error` |
| `result` | `Ok`: the charger's raw CALLRESULT payload; otherwise a reason string or `null` |

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

### 4.3 `command.authorize.response`

Reply to a `charger.authorize.requested` event. Must be published **within 10 seconds** or the vehicle is automatically rejected.

```json
{
  "ocppId": "CP-001",
  "status": "Accepted"
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `ocppId` | `string` | yes | Station identity from the event |
| `status` | `string` | yes | `Accepted` — vehicle may charge; `Rejected` — vehicle refused |

**Flow when `Accepted`:** OCPPServer responds `Accepted` to the charger's `Authorize.req`. The charger starts the session automatically and sends `StartTransaction` / `TransactionEvent(Started)` — no `command.remote.start` needed.

**Flow when `Rejected` (or timeout):** OCPPServer responds `Rejected`. Most chargers remain in `Preparing` status waiting for another authorization method (RFID card, app). At that point the normal `charger.status.changed { status: "Preparing" }` event fires and the usual remote-start flow applies.

### 4.4 `command.commandreq`

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

### 4.5 `command.statusreq`

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

### 4.6 `command.charging.limit` (OCPP 1.6)

Limit the charging current. camelCase, read by exact name.

```json
{ "ocppId": "03012", "limitA": 16, "transactionId": 1 }
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `ocppId` | `string` | yes | Station identity |
| `limitA` | `number` | yes | Amps, 6–80 (IEC 61851 minimum 6 A), rounded to 0.1 |
| `transactionId` | `int?` | no | Running transaction to limit immediately |

Sends `SetChargingProfile`:
1. `TxDefaultProfile`, connector 0, id 1001, stack 0, kind `Relative`, unit `A` — all future transactions; stored by the charger.
2. If `transactionId` is given and (1) was accepted: `TxProfile`, connector 1, id 1002 — the running session.

A `Rejected` `Relative` profile is retried once as `Absolute` (`startSchedule` = now). Fixed ids mean a new limit
replaces the previous one. Publishes `charger.charging.limit.response`.

### 4.7 `command.charging.clear` (OCPP 1.6)

```json
{ "ocppId": "03012" }
```

Sends `ClearChargingProfile` for purpose `TxDefaultProfile`, then `TxProfile` → no limit. Publishes
`charger.charging.limit.response` with `limitA: null`.

### 4.8 `command.dev.call` (developer tooling, OCPP 1.6)

Raw OCPP call for development / diagnostics. camelCase.

```json
{ "ocppId": "03012", "requestId": "7f1c…", "action": "ChangeConfiguration",
  "payload": { "key": "MeterValueSampleInterval", "value": "60" } }
```

Whitelisted actions (`DevCalls.AllowedActions`): `GetConfiguration`, `ChangeConfiguration`, `TriggerMessage`,
`DataTransfer`, `Reset`, `UnlockConnector`, `ClearCache`, `GetCompositeSchedule`, `GetLocalListVersion`.
Transaction and charging-profile actions are deliberately excluded — they have validated commands of their own.
Minimal payload shape is validated (e.g. `Reset.type` ∈ Soft/Hard). Publishes `charger.dev.call.response`.

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
    "ocppId":        "CP-001",
    "status":        "Charging",
    "isOnline":      true,
    "isFastCharger": false,
    "maxPower":      22,
    "vendor":        "Wall Box Chargers",
    "chargePointModel": "PLP1-W-2-4-8",
    "firmwareVersion":  "6.2.0",
    "ocppVersion":   "ocpp1.6",
    "lastStatusUpdate": "2026-05-25T14:30:00Z",
    "createdAt":     "2026-01-15T09:00:00Z",
    "isConnected":   true,
    "meterValueWh":  15320.50,
    "meterStartWh":  12500.00,
    "currentPowerKw": 7.4,
    "stateOfCharge": 82.5,
    "meterType":     "External MID"
  }
]
```

| Field | Type | Source | Notes |
|---|---|---|---|
| `ocppId` | `string` | DB | Station OCPP identity |
| `status` | `string` | DB | Last known OCPP status |
| `isOnline` | `bool` | DB | Updated by BootNotification and Heartbeat |
| `isFastCharger` | `bool` | DB | Admin-set flag |
| `maxPower` | `int` | DB | Admin-set max power in kW |
| `vendor` | `string?` | DB | From BootNotification |
| `chargePointModel` | `string?` | DB | From BootNotification |
| `firmwareVersion` | `string?` | DB | From BootNotification |
| `ocppVersion` | `string` | DB | Negotiated OCPP protocol |
| `lastStatusUpdate` | `datetime?` | DB | Timestamp of last DB write |
| `createdAt` | `datetime` | DB | First seen |
| `isConnected` | `bool` | Live | WebSocket currently open |
| `meterValueWh` | `decimal?` | Live | Latest energy reading (Wh); `null` if no session |
| `meterStartWh` | `decimal?` | Live | Session start meter (Wh); `null` if no active session |
| `currentPowerKw` | `double?` | Live | Instantaneous power; `null` if unavailable |
| `stateOfCharge` | `decimal?` | Live | Battery SoC %; DC fast chargers only |
| `meterType` | `string?` | Live | `"Internal"`, `"External MID"`, or `"Internal NON compliant"` (Wallbox); `null` if not connected |

> **Live fields** are sourced from the in-memory `ConnectorState` and are only present while the charger is connected. They reset to `null` on server restart.

### 5.2 Get Plug Detail

```
GET /api/admin/plugs/{ocppId}
```

**Path parameters:**

| Parameter | Type | Notes |
|---|---|---|
| `ocppId` | `string` | Station OCPP identity |

**Response 200:** Same object shape as a single item from the list above, plus `chargePointSN` and `SIMNr` fields.

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
  "maxPower":      22
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `isFastCharger` | `bool` | yes | Marks station as fast charger |
| `maxPower` | `int` | yes | Maximum power in kW |

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
  "location":      "ftp://diagnostics.example.com/uploads/",
  "startTime":     "2026-05-01T00:00:00Z",
  "stopTime":      "2026-05-25T23:59:59Z",
  "retries":       3,
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

### 5.5 List Error Logs

```
GET /api/admin/error-logs
```

Returns persisted trace entries from the `ErrorLogs` database table, written by `TracingService`. Results are ordered newest-first.

**Query parameters:**

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `chargePointId` | `string` | *(all)* | Filter to a single charger by OcppId (exact match) |
| `minLevel` | `string` | `Warning` | Minimum severity: `Info` \| `Warning` \| `Error` \| `Critical` |
| `limit` | `int` | `200` | Maximum number of rows to return |

**Examples:**
```
GET /api/admin/error-logs
GET /api/admin/error-logs?chargePointId=u030
GET /api/admin/error-logs?minLevel=Error
GET /api/admin/error-logs?chargePointId=u030&minLevel=Error&limit=50
```

**Response 200:**
```json
[
  {
    "id":             42,
    "chargePointId":  "u030",
    "sessionId":      17,
    "occurredAt":     "2026-05-26T08:51:36Z",
    "level":          "Error",
    "source":         "Watcher",
    "message":        "u030: no message for 180s — aborting socket",
    "isSolved":       false,
    "solvedAt":       null
  }
]
```

| Field | Type | Notes |
|---|---|---|
| `id` | `int` | Primary key |
| `chargePointId` | `string?` | OcppId of the affected charger; `null` for infrastructure events |
| `sessionId` | `int?` | Active `LocalTxId` at the time of the event; `null` outside a session |
| `occurredAt` | `datetime` | UTC timestamp when the event was recorded |
| `level` | `string` | Severity name: `Info`, `Warning`, `Error`, `Critical` |
| `source` | `string` | Subsystem tag (e.g. `"Watcher"`, `"OCPP"`, `"RemoteStart"`, `"Authorize"`) |
| `message` | `string` | Human-readable description; for `Critical` entries includes the exception type and full stack trace |
| `isSolved` | `bool` | Admin-settable resolved flag; always `false` on creation |
| `solvedAt` | `datetime?` | UTC timestamp when marked solved; `null` if not yet resolved |

> Rows older than `Tracing:RetentionDays` (default 3) are automatically deleted daily by `ErrorLogCleanupService` at `Tracing:CleanupHourUtc` (default 03:00 UTC).

### 5.6 Mark Error Log as Solved

```
PATCH /api/admin/error-logs/{id}/solve
```

Marks a single entry as resolved. Sets `isSolved = true` and records the current UTC time in `solvedAt`. Idempotent — calling it on an already-solved entry returns `200` without modifying `solvedAt` again.

**Path parameters:**

| Parameter | Type | Notes |
|---|---|---|
| `id` | `int` | Primary key of the `ErrorLog` row |

**Response 200:**
```json
{ "id": 42, "isSolved": true, "solvedAt": "2026-05-26T10:15:00Z" }
```

**Response 404:** No entry with the given `id`.

---

### 5.7 List Trace Files

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

### 5.8 Download Trace File

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
    "Host":       "rabbitmq.example.com",
    "Port":       5672,
    "Username":   "ocpp",
    "Password":   "secret",
    "VirtualHost": "/"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=pg.example.com;Database=charging;Username=ocpp;Password=secret"
  },
  "Trace": {
    "Directory":   "logs/traces",
    "DownloadKey": "<secret-key-for-trace-download>"
  },
  "Tracing": {
    "MinPersistLevel": "Warning",
    "RetentionDays":   3,
    "CleanupHourUtc":  3
  },
  "ChargerWatcher": {
    "CheckIntervalSeconds":       30,
    "InactivityThresholdSeconds": 180
  }
}
```

| Key | Notes |
|---|---|
| `RabbitMq:Host` | RabbitMQ broker hostname |
| `RabbitMq:Port` | Default `5672` |
| `RabbitMq:Username` / `Password` | Broker credentials |
| `RabbitMq:VirtualHost` | Default `/`. Lets a second deployment (e.g. `ocpp-home`) share one broker in isolation |
| `Cors:AllowedOrigins` | Array of admin UI origins. Default `["https://admin.alternatiview.com.ua"]` |
| `Trace:DownloadKey` | Shared secret for the `/api/admin/traces` endpoints |
| `Tracing:MinPersistLevel` | Minimum `ErrorLogLevel` written to the `ErrorLogs` table. Values: `Info`, `Warning`, `Error`, `Critical`. Default `Warning`. |
| `Tracing:RetentionDays` | Error log rows older than this many days are deleted by the daily cleanup job. Default `3`. |
| `Tracing:CleanupHourUtc` | UTC hour (0–23) when the daily cleanup runs. Default `3` (03:00 UTC). |
| `ChargerWatcher:CheckIntervalSeconds` | How often the watcher scans all connected chargers (default `30`) |
| `ChargerWatcher:InactivityThresholdSeconds` | Seconds of silence before a charger is considered dead (default `180`). With a 60-second heartbeat interval this equals ~3 missed heartbeats. |

---

## 7. Sequence Flows

### 7.1 ISO 15118 Auto-Authorize (DC fast charger, vehicle identity)

```
Backend                    OCPPServer                  Charger
   │                           │                          │
   │                           │◄── Authorize.req ────────│
   │                           │    { idTag: "VID:ABC" }  │
   │                           │                          │
   │◄── charger.authorize.requested ──│                   │
   │    { ocppId, carId: "ABC" }      │                   │
   │                           │      (10 s window)       │
   │── command.authorize.response ───►│                   │
   │   { ocppId, status: "Accepted" } │                   │
   │                           │── Authorize.conf ────────►│
   │                           │   { status: "Accepted" } │
   │                           │                          │ (starts automatically)
   │                           │◄── StartTransaction ─────│
   │◄── charger.transaction.started ──│                   │
```

If backend responds `Rejected` or does not respond within 10 s:
```
   │                           │── Authorize.conf ────────►│
   │                           │   { status: "Rejected" } │
   │                           │                          │ (falls back to Preparing)
   │                           │◄── StatusNotification ───│
   │                           │    { status: "Preparing" }│
   │◄── charger.status.changed ──│                         │
   │    { status: "Preparing",     │                       │
   │      carId: "ABC" }           │                       │
```

### 7.2 Remote Start Session

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

### 7.3 Remote Stop Session

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

### 7.4 Organic Session (charger-initiated)

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

### `charger.authorize.requested` Timeout

- OCPPServer holds the charger's `Authorize.req` open while waiting.
- If `command.authorize.response` is not received within **10 seconds**, the server responds `Rejected` to the charger and logs a warning.
- Design your backend consumer to respond in under 2–3 seconds to leave headroom.

### REST API

- All 4xx/5xx responses include a JSON body `{ "error": "<description>" }`.
- The diagnostics endpoint returns `404` if the charger is not currently connected (cannot deliver the OCPP command).

### Connection Loss

- When a charger disconnects (clean close or watcher-triggered abort), OCPPServer emits `charger.status.changed` with `isConnected: false`.
- In-flight command waits (remote start/stop, authorize) will time out and emit a `Timeout` or `Rejected` response event.

### Silent Disconnects and the Charger Watcher

Some chargers drop the TCP connection without sending a WebSocket Close frame (network loss, SIM card reset, firmware hang). The server's WebSocket receive loop hangs waiting for data that never arrives. `ChargerWatcherService` resolves this:

- Every `ChargerWatcher:CheckIntervalSeconds` (default 30 s) it scans all connected stations.
- Any station whose `ConnectorState.LastMessageAt` is older than `ChargerWatcher:InactivityThresholdSeconds` (default 180 s) is treated as dead.
- `Plug.IsOnline` is set to `false` in the database.
- `WebSocket.Abort()` is called on the stale socket, which causes the Program.cs receive loop to exit and fire the standard `finally` cleanup — `Remove()` and `PushDisconnect()` — emitting `charger.status.changed { isConnected: false }` exactly once.

The 60-second heartbeat interval means a 180-second threshold ≈ 3 missed heartbeats before the watcher acts.
