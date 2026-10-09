# EVHomeAPI — REST API

Base URL: `https://home-api.alternatiview.com.ua`

Auth: `Authorization: Bearer <JWT>` from register/login (30-day TTL). 🔒 = JWT required.
Admin endpoints use header `X-Admin-Key: <AdminKey>` instead.

Errors: plain-text body with a short message; validation errors are RFC 7807 `ProblemDetails`.

## Health

| Method | Path | Response |
|---|---|---|
| GET | `/api/health` | `200 {"status":"ok","db":true}` · `503 {"status":"degraded","db":false}` |

## Auth

| Method | Path | Body | Response |
|---|---|---|---|
| POST | `/api/auth/register` | `{name, email, password}` (password ≥ 8) | `200 {token, name, email}` · `409` email in use |
| POST | `/api/auth/login` | `{email, password}` | `200 {token, name, email}` · `401` |
| GET 🔒 | `/api/auth/profile` | — | `200 {name, email}` |
| PUT 🔒 | `/api/auth/profile` | `{name, email, newPassword?, currentPassword?}` | `200 {name, email}` · `400` wrong current password · `409` email in use |

Emails are case-insensitive (stored lower-case). Rate limit: 10 req/min per client IP on register/login → `429`.

## Stations

| Method | Path | Body | Response |
|---|---|---|---|
| GET 🔒 | `/api/stations` | — | `200 [StationDto]` — stations the user owns or is a member of |
| GET 🔒 | `/api/stations/{id}` | — | `200 StationDto` · `404` (also when no access) |
| POST 🔒 | `/api/stations/claim` | `{ocppId, claimCode}` | `200 StationDto` (role `Owner`) · `400` invalid ID/code/already claimed |

`StationDto`: `{id, ocppId, name, role: "Owner"|"Member", claimedAt, isOnline, connectorStatus, lastStatusAt}`
— `connectorStatus` is connector 1 in OCPP 1.6 vocabulary (`Available`, `Preparing`, `Charging`, `SuspendedEV`, `Finishing`, `Faulted`, …).

Claim codes: format `XXXX-XXXX`, case and dashes ignored, **one-time**. Unknown station, wrong code
and already-claimed return the same `400` (no station-ID discovery). Rate limit: 5 req/min per client IP.

## Charging

| Method | Path | Body | Response |
|---|---|---|---|
| POST 🔒 | `/api/stations/{id}/start` | `{connectorId?: 1}` | `202 SessionDto` (Pending) · `409` offline / session in progress · `503` gateway down |
| POST 🔒 | `/api/stations/{id}/stop` | — | `202 SessionDto` (Stopping; idempotent) · `200 SessionDto` (Completed — a Paused session with nothing charging is ended at once) · `409` idle, still starting or resuming |
| GET 🔒 | `/api/stations/{id}/session` | — | `200 SessionDto` in progress · `204` idle |
| GET 🔒 | `/api/stations/{id}/sessions?limit=50` | — | `200 [SessionDto]` newest first (max 200) |
| GET 🔒 | `/api/sessions/{id}` | — | `200 SessionDto` · `404` |
| GET 🔒 | `/api/sessions/{id}/meter-history` | — | `200 [{elapsedSec, currentPowerKw, soc}]` ~1 sample / 30 s |

### Current limit

| Method | Path | Body | Response |
|---|---|---|---|
| PUT 🔒 | `/api/stations/{id}/limit` | `{limitA: number \| null}` — `null` = at `maxCurrentA` | `202 StationDto` (`limitStatus: "Pending"`) · `400` outside 6…`maxCurrentA` · `403` not owner · `409` offline · `503` gateway down |

`StationDto` adds `maxCurrentA` (installation maximum, set by admin, default 32), `currentLimitA`
(`null` = at the installation maximum — **never unlimited**: the charger default, e.g. 32 A, may exceed the
house supply; the effective limit is also pushed when the station is claimed) and `limitStatus` (`Pending` → `Applied` | `Rejected` | `NotSupported` | `Timeout` | `Error`).
EVOCPP sets a `TxDefaultProfile` (all future sessions, stored on the charger) and, if a session is running,
a `TxProfile` for it. The charger's answer arrives as SignalR `ChargingLimitUpdated {stationId, limitA, status}`;
answers to a superseded request are ignored.

`SessionDto`: `{id, stationId, status, initiatedBy: "App"|"Charger", stopReason?: "UserInitiated"|"ChargerInitiated"|"PowerLoss", createdAt, startedAt, endedAt, energyKwh, currentPowerKw, soc, transactionId}`
— `energyKwh` covers all transactions of the session; `transactionId` is `null` while Paused between transactions.

Session lifecycle:

```
App start:     Pending ──SessionStarted──► Active ──stop──► Stopping ──SessionFinalized──► Completed
                  └──SessionStartFailed (Rejected|Timeout)──► Cancelled      └─SessionStopFailed──► Active
Charger start (button / RFID / LOCAL):     Active (owner) ─────────────────────────────────► Completed

Power loss:    Active ──charger offline / StopTransaction(PowerLoss)── SessionPaused ──► Paused
               Paused ──charger back, car plugged: RemoteStart → SessionStarted (same id)──► Active
               Paused ──unplugged / 3 failed attempts / > 24 h── SessionFinalized(PowerLoss) ──► Completed
               Paused ──stop──► Completed (UserInitiated)
```

`start`/`stop` return immediately; the outcome arrives over SignalR. A Pending or Stopping session
with no answer from EVOCPP for 3 min is resolved by the watchdog (`Timeout`); a Stopping session whose
charger is offline is completed instead. Paused sessions are driven by `SessionResumeService`
(events + watchdog), see architecture D12.
One open session per station (Pending/Active/Stopping/Paused) is enforced by a unique filtered index.

## Developer tools (owner only; 404 unless `DevTools:Enabled`)

| Method | Path | Body | Response |
|---|---|---|---|
| POST 🔒 | `/api/stations/{id}/dev/call` | `{action, payload?}` | `200 {action, status, result, elapsedMs}` · `400` action not allowed · `504` no answer within `DevTools:CallTimeoutSeconds` |
| GET 🔒 | `/api/stations/{id}/dev/charger` | — | EVOCPP's raw plug JSON (firmware, meterType, protocol, last heartbeat…) |
| POST 🔒 | `/api/stations/{id}/dev/push` | `{kind}` — `Paused` \| `Resumed` \| `ResumeFailed` \| `Completed` | `200 {enabled, devices, sent}` — sample push to all devices of the station users, sent synchronously |

`action` ∈ `GetConfiguration`, `ChangeConfiguration`, `TriggerMessage`, `DataTransfer`, `Reset`, `UnlockConnector`,
`ClearCache`, `GetCompositeSchedule`, `GetLocalListVersion` — `payload` is the OCPP 1.6 request body as is.
`status`: `Ok` (charger answered; `result` = its raw CALLRESULT payload), `Invalid`, `NotConnected`, `NotSupported`,
`Timeout`, `Error`. Routed via EVOCPP `command.dev.call` / `charger.dev.call.response`.

## Push notifications (FCM)

| Method | Path | Body | Response |
|---|---|---|---|
| POST 🔒 | `/api/devices` | `{token, platform?: "android", language?: "uk"\|"en"}` | `204` — register / refresh this install's FCM token (moves to the caller if another account had it) |
| POST 🔒 | `/api/devices/unregister` | `{token}` | `204` — on logout |

EVHomeAPI sends straight to FCM HTTP v1 (service account `Fcm:ServiceAccountJsonBase64`; empty = push off)
from an in-memory queue (`PushWorker`) to every device of every user with access to the station. Texts are
rendered server-side in the device language. Events: `Paused` (charger lost power), `Resumed`, `ResumeFailed`
(unplugged / car did not restart / pause > 24 h), `Completed` (finished by the charger or the car — not when
stopped from the app). FCM `data`: `{stationId, kind}`; Android channel `charging`. Tokens that FCM reports
as unregistered are deleted.

## SignalR — `/hubs/charger`

JWT via `?access_token=<token>`. After connecting call `JoinStation(stationId)` (fails with
`HubException "No access to this station."` without access) and `LeaveStation(stationId)`.
Event names and payloads are identical to the commercial Nabla app (`totalCost` is always `null`):

| Event | Payload |
|---|---|
| `StatusUpdated` | `{stationId, ocppConnectorId, status, isConnected, carId}` |
| `SessionStarted` | `{stationId, sessionId, transactionId, meterStartWh}` |
| `SessionStartFailed` | `{stationId, sessionId, reason: "Rejected"\|"Timeout"}` |
| `MeterUpdated` | `{stationId, sessionId, energyKwh, currentPowerKw, totalCost: null, soc}` |
| `SessionFinalized` | `{stationId, sessionId, energyKwh, totalCost: null, stopReason}` |
| `SessionStopFailed` | `{stationId, sessionId, reason: "Rejected"\|"Timeout"}` |
| `ChargingLimitUpdated` | `{stationId, limitA, status}` — Nabla Home only |
| `SessionPaused` | `{stationId, sessionId, reason: "ChargerOffline"\|"PowerLoss"}` — Nabla Home only; resume arrives as `SessionStarted` with the same `sessionId` |

## EVOCPP integration (RabbitMQ vhost `/home`)

- Consumes every `charger.#` event from one durable queue `evhome.events` with prefetch 1
  (strict publish order). Unknown/unclaimed stations are ignored.
- Publishes `command.remote.start` (`IdTag = "U{userId}"`, `TrackingId` for correlation),
  `command.remote.stop`, `command.authorize.response`.
- `charger.booted` → re-pushes the effective current limit (a reboot may wipe charging profiles).
- `charger.transaction.stopped` with `reason: "PowerLoss"` → session Paused, not Completed.
- ISO 15118 `charger.authorize.requested` → `Accepted` if the station has an owner, else `Rejected`.
- Regular idTags (RFID, `LOCAL` button) are accepted by EVOCPP itself; EVHomeAPI attributes the
  resulting transaction to the station owner.
- Casing: commands are PascalCase (EVOCPP deserializes case-sensitively), except
  `command.authorize.response` which EVOCPP reads as exact camelCase `ocppId`/`status`.

## Admin (`X-Admin-Key`)

| Method | Path | Body | Response |
|---|---|---|---|
| POST | `/api/admin/stations` | `{ocppId, name, maxCurrentA?: 32}` | `200 {id, ocppId, name, claimCode}` · `409` exists |
| PUT | `/api/admin/stations/{ocppId}/max-current` | `{maxCurrentA}` (6–80) | `200 {ocppId, maxCurrentA, currentLimitA, limitStatus, pushed}` — lowers a higher user limit, pushes the effective limit if online |
| POST | `/api/admin/stations/{ocppId}/claim-code` | — | `200 {…, claimCode}` new code, old one invalid |

The claim code appears **only** in these responses; the database stores a BCrypt hash.

```bash
curl -s -X POST https://home-api.alternatiview.com.ua/api/admin/stations \
  -H "X-Admin-Key: $HOME_ADMIN_KEY" -H "Content-Type: application/json" \
  -d '{"ocppId":"30011","name":"DIY Garage"}'
```

## Client IP and rate limiting

Behind Cloudflare Tunnel all requests come from cloudflared on localhost, so the limiter keys on
`CF-Connecting-IP`. The API port is bound to `127.0.0.1` on the Pi, so the header cannot be
spoofed from the LAN.
