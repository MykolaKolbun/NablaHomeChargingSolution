# OCPP Server

OCPP 1.6J / 2.0 / 2.0.1 / 2.1 WebSocket gateway for EV charger communication.

## Charger Connection

Configure your charger's OCPP backend URL to:

```
wss://ocpp.alternatiview.com.ua/ws/{chargePointId}
```

**Example for charger u030:**
```
wss://ocpp.alternatiview.com.ua/ws/u030
```

TLS is terminated by Cloudflare Tunnel — the server itself runs plain WebSocket internally.

The server negotiates the OCPP sub-protocol at handshake (`Sec-WebSocket-Protocol`). Supported versions in priority order: `ocpp2.1` › `ocpp2.0.1` › `ocpp2.0` › `ocpp1.6`. Chargers that send no sub-protocol header are treated as OCPP 1.6.

> The `chargePointId` in the URL is normalised to lowercase on connect. The value is stored as-is in the `Plugs` table on first `BootNotification`.

---

## Viewing Logs on the Pi

Follow live logs (Ctrl+C to exit):
```bash
docker logs evchargingapi-ocpp-1 --tail 50 -f
```

Last 100 lines without following:
```bash
docker logs evchargingapi-ocpp-1 --tail 100
```

When a charger connects successfully you should see:
```
Station u030 connected — protocol: ocpp1.6
Received CALL: BootNotification from u030
```

---

## Admin API

Base URL: `https://ocpp.alternatiview.com.ua`

### List all plugs

```
GET /api/admin/plugs
```

Returns all registered chargers with live session data merged in.

```json
[
  {
    "ocppId":          "u030",
    "status":          "Charging",
    "isOnline":        true,
    "isFastCharger":   false,
    "maxPower":        22,
    "vendor":          "Wall Box Chargers",
    "chargePointModel": "PLP1-W-2-4-8",
    "firmwareVersion": "6.2.0",
    "ocppVersion":     "ocpp1.6",
    "lastStatusUpdate": "2026-05-26T08:00:00Z",
    "createdAt":       "2026-01-15T09:00:00Z",
    "isConnected":     true,
    "meterValueWh":    15320.50,
    "meterStartWh":    12500.00,
    "currentPowerKw":  7.4,
    "stateOfCharge":   null,
    "meterType":       "External MID"
  }
]
```

`isConnected`, `meterValueWh`, `meterStartWh`, `currentPowerKw`, `stateOfCharge`, and `meterType` are live in-memory fields — they are `null` when the charger is not connected.

---

### Get single plug

```
GET /api/admin/plugs/{ocppId}
```

Same shape as the list above, plus `chargePointSN` and `sIMNr` fields.

Returns `404` if the `ocppId` is not in the database.

---

### Update plug settings

```
PUT /api/admin/plugs/{ocppId}
Content-Type: application/json
```

Admin-editable fields only. Hardware fields (vendor, model, firmware, SN) are written automatically from `BootNotification` and cannot be overridden here.

```json
{
  "isFastCharger": false,
  "maxPower":      22
}
```

| Field | Type | Notes |
|---|---|---|
| `isFastCharger` | `bool` | `true` = DC fast charger (enables SoC display, ISO 15118 flows) |
| `maxPower` | `int` | Maximum power in **kW** — e.g. `22` = 22 kW AC, `150` = 150 kW DC |

Returns `200` with `{ ocppId, isFastCharger, maxPower }` on success, `404` if not found.

---

### Request diagnostics upload

```
POST /api/admin/plugs/{ocppId}/diagnostics
Content-Type: application/json
```

Sends a `GetDiagnostics` OCPP command. The charger uploads its log file to the specified location and sends `DiagnosticsStatusNotification` updates (forwarded to RabbitMQ as `charger.diagnostics.status`).

```json
{
  "location":      "ftp://diagnostics.example.com/uploads/",
  "startTime":     "2026-05-01T00:00:00Z",
  "stopTime":      "2026-05-26T23:59:59Z",
  "retries":       3,
  "retryInterval": 60
}
```

Only `location` is required. Returns `503` if the charger is not currently connected.

---

### List error logs

```
GET /api/admin/error-logs
```

Returns structured trace entries persisted by the server. Ordered newest-first.

Optional query parameters:

| Parameter | Default | Notes |
|---|---|---|
| `chargePointId` | *(all)* | Filter to one charger |
| `minLevel` | `Warning` | `Verbose` / `Info` / `Warning` / `Error` / `Critical` |
| `limit` | `200` | Max rows returned |

```
GET /api/admin/error-logs?chargePointId=u030&minLevel=Error&limit=50
```

---

### List trace files

```
GET /api/admin/traces?key=<secret>
```

Returns the rolling `.txt` trace files written to disk by the server. The `key` must match `Trace:DownloadKey` in server config.

---

### Download trace file

```
GET /api/admin/traces/{filename}?key=<secret>
```

Returns the file as `text/plain`. The file can be read while the server is still writing to it.

---

## Further Reading

- `docs/messaging-api-spec.md` — full RabbitMQ event/command contracts + REST API reference
- `docs/database-schema.md` — Plugs and ErrorLogs table design, migration history
- `docs/flows-explained.md` — sequence diagrams for remote start, stop, authorize, silent disconnect
- `docs/gaps.md` — known issues and deferred work
