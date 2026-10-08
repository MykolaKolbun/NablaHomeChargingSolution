# OCPPServer — Flow Explanations

Detailed plain-language explanations of every message flow in the system.
For sequence diagrams and payload schemas see [ocpp-server-overview.md](ocpp-server-overview.md) and [messaging-api-spec.md](messaging-api-spec.md).

---

## Table of Contents

1. [Charger connects and boots](#1-charger-connects-and-boots)
2. [ISO 15118 auto-authorize (vehicle identity)](#2-iso-15118-auto-authorize-vehicle-identity)
3. [Remote start (backend-initiated)](#3-remote-start-backend-initiated)
4. [Remote stop (backend-initiated)](#4-remote-stop-backend-initiated)
5. [Organic session (charger-initiated)](#5-organic-session-charger-initiated)
6. [Meter updates during a session](#6-meter-updates-during-a-session)
7. [Charger disconnects](#7-charger-disconnects)
8. [Silent disconnect (watcher-detected)](#8-silent-disconnect-watcher-detected)

---

## 1. Charger connects and boots

### What triggers it

Every time a physical charger powers on, restarts, or loses and re-establishes its network connection it goes through this flow. It is the charger saying "I am here, this is who I am."

### Step by step

**1. WebSocket upgrade**

The charger opens a TCP connection and upgrades it to WebSocket at `ws://<server>/ws/<stationId>`, where `<stationId>` is the charger's own OCPP identity — typically its serial number configured in the charger's settings.

The charger includes a `Sec-WebSocket-Protocol` header listing the OCPP versions it supports, for example `ocpp2.1, ocpp1.6`. The server picks the highest version it can handle and echoes it back. This negotiated protocol is stored immediately in the `ConnectorState` for this station and persisted to `Plugs.OcppVersion` on the first BootNotification.

If the charger sends no protocol header, the server falls back to `ocpp1.6`.

**2. BootNotification**

Once the WebSocket is open the charger sends `BootNotification`, which carries the charger's hardware information: vendor, model, serial number, firmware version, and SIM card ICCID if present. This is the charger introducing itself.

The server upserts the `Plugs` database row for this `stationId`:
- If the charger has never been seen before, a new row is created with all the reported fields.
- If it already exists, the server only overwrites fields for which the charger sent a non-empty value. This preserves admin-entered data (e.g. `IsFastCharger`, `MaxPower`) and avoids clearing optional fields that a charger omits on reboot.

`IsOnline` is set to `true` and `OcppVersion` is updated. The vendor name is used to select the appropriate `IChargerAdapter` implementation (e.g. `WallboxChargerAdapter` for "Wall Box Chargers"), which is stored in the `ConnectorState` for this session. The adapter handles any vendor-specific quirks from that point on — including reading Wallbox's proprietary `meterType` field from the BootNotification payload itself.

The server responds `Accepted` and the charger begins normal operation.

**3. StatusNotification**

Immediately after BootNotification most chargers report their current connector status (e.g. `Available`, `Charging`) via `StatusNotification`. The server updates `Plug.Status` in the database and publishes `charger.status.changed` to RabbitMQ so the backend can update its own state and notify connected app users.

### What can go wrong

- If the `stationId` in the URL does not match what the charger has configured in its OCPP settings, the charger will connect but `BootNotification` payloads will be inconsistent. Always ensure the WebSocket URL path matches the charger's configured Central System URL.
- Some chargers cache the old WebSocket and do not send a new BootNotification after a brief outage. The server's existing `ConnectorState` is discarded on any disconnect; if the charger reconnects without re-booting, it will not send BootNotification again and the adapter will remain the default. This is normal — it only matters if the vendor changes between connections, which never happens in practice.

---

## 2. ISO 15118 auto-authorize (vehicle identity)

### What triggers it

When a vehicle capable of ISO 15118 or DIN 70121 communication (primarily DC fast charging) plugs into a compatible charger, the charger reads the vehicle's **EVCC ID** — the MAC address of the vehicle's on-board communication controller. The charger then asks the CSMS whether this vehicle is allowed to charge. This is the "Plug & Charge" or "Autocharge" scenario.

This flow only occurs on **DC fast chargers** equipped with ISO 15118 support. AC chargers cannot read the vehicle identity.

### Step by step

**1. Vehicle plugs in**

The charger detects the cable and initiates an ISO 15118 or DIN 70121 handshake with the vehicle over the pilot line (CCS). During this handshake it reads the vehicle's EVCC ID — a 6- or 8-byte hex MAC address.

**2. Charger sends `Authorize.req`**

The charger sends an `Authorize.req` to the server carrying the vehicle identity:
- **OCPP 1.6:** `idTag = "VID:001681020001"` — the `VID:` prefix is a convention defined in the ISO 15118 OCPP whitepaper to distinguish vehicle IDs from user RFID tokens.
- **OCPP 2.x:** `idToken = { "idToken": "001681020001", "type": "MacAddress" }` — explicit type field. Some chargers also combine RFID + vehicle ID using the `additionalInfo` array.

The server detects this is a vehicle identity token (not a regular RFID/app token), extracts the EVCC ID, stores it in `ConnectorState.CarId`, and **holds the connection open** while it asks the backend.

**3. Server publishes `charger.authorize.requested`**

The server publishes `charger.authorize.requested` to RabbitMQ with the `ocppId` and `carId`. The charger is waiting for a response — the server has a **10-second window** to get an answer back before it times out and rejects the vehicle.

**4. Backend decides**

The backend receives the event, looks up whether this EVCC ID is registered to a user account, and decides whether to allow charging. It must publish `command.authorize.response` with `status: "Accepted"` or `status: "Rejected"`.

**Design your backend to respond in under 2–3 seconds** to leave headroom. The 10-second window exists for slow database lookups or cold starts, not as a target latency.

**5a. Accepted — vehicle charges automatically**

The server receives `Accepted`, responds to the charger's `Authorize.req` with `Accepted`, and the charger starts the charging session on its own. The charger sends `StartTransaction` / `TransactionEvent(Started)` shortly after. No `command.remote.start` from the backend is needed — the vehicle authorized itself.

This is the fully automatic path: **vehicle plugs in → charges** with no user interaction required.

**5b. Rejected (or timeout) — falls back to normal flow**

If the backend responds `Rejected`, or if no response arrives within 10 seconds (fail-closed by design), the server sends `Rejected` back to the charger.

Most DC fast chargers will keep the cable locked and transition to `Preparing` status, showing an error on the display ("Authorization failed — please use RFID or app"). The server publishes `charger.status.changed { status: "Preparing", carId: "001681020001" }` — the `carId` is still included so the backend knows which vehicle is waiting. The backend can now send `command.remote.start` through the normal flow if it wants to start the session via other means.

Some chargers may instead disconnect immediately and return to `Available`. In that case the user would need to unplug and try again.

### Why fail-closed on timeout?

If the backend is unreachable and the server defaulted to `Accepted`, unregistered vehicles could start sessions with no linked user account, making billing impossible. Defaulting to `Rejected` ensures every session is accountable.

---

## 3. Remote start (backend-initiated)

### What triggers it

A user opens the app, selects a charger, and taps "Start Charging." The app sends a request to the backend, which then instructs the OCPP server to start a session on the charger. This is the standard flow for AC chargers (which cannot read vehicle identity) and for users without ISO 15118-capable vehicles.

### Step by step

**1. Backend publishes `command.remote.start`**

The backend publishes to `ocpp.commands` with routing key `command.remote.start` and a payload containing `OcppId`, `ConnectorId`, and `IdTag` (the user's authorization token, typically their RFID card UID or app-generated UUID).

**2. Server sends `RemoteStartTransaction` to charger**

The server looks up the `ConnectorState` for this `OcppId`, retrieves the open `WebSocket`, and sends an OCPP `RemoteStartTransaction` request to the charger. For OCPP 2.x chargers it sends `RequestStartTransaction` instead (same semantics, different action name).

The charger's response to this request is just an acknowledgement — `Accepted` means the charger will *try* to start, not that the session has started.

**3. Waiting for `StartTransaction`**

If the charger responds `Accepted`, the server registers a `TaskCompletionSource<int>` in `ConnectorState.PendingStart` and waits up to **60 seconds** for the charger to send `StartTransaction.req` (or `TransactionEvent(Started)` for OCPP 2.x). This is the actual confirmation that the session is live.

The 60-second window exists because the user may still need to physically plug in the cable or tap an RFID card at the charger. The remote command tells the charger to be ready; the session starts when the physical action happens.

**4. Session starts**

When `StartTransaction` arrives, the server:
- Assigns a local transaction ID and stores it in `ConnectorState.LocalTxId`
- Records the meter start reading in `ConnectorState.MeterStartWh`
- Completes the `PendingStart` TCS, unblocking the waiting handler
- Publishes `charger.transaction.started`
- Publishes `charger.remote.start.response { status: "Accepted" }`

**Failure cases:**
- **Charger responds `Rejected`:** published immediately as `charger.remote.start.response { status: "Rejected" }`. No waiting.
- **No `StartTransaction` within 60 s:** server cancels the pending TCS, publishes `charger.remote.start.response { status: "Timeout" }`. The user may not have plugged in the cable in time.

### Important: two separate events

`charger.remote.start.response` and `charger.transaction.started` are both published on success. The backend should wait for `charger.transaction.started` to confirm a session is truly live before creating a billing record. `charger.remote.start.response { status: "Accepted" }` only means "StartTransaction was received" — the transaction ID is in `charger.transaction.started`.

---

## 4. Remote stop (backend-initiated)

### What triggers it

A user taps "Stop Charging" in the app, or the backend stops the session for a business reason (billing limit reached, reservation expired, emergency stop).

### Step by step

**1. Backend publishes `command.remote.stop`**

The backend publishes `command.remote.stop` with `OcppId` and `TransactionId`. The `TransactionId` should be the value received in the earlier `charger.transaction.started` event.

**2. Server sends `RemoteStopTransaction` to charger**

For OCPP 1.6 chargers: `RemoteStopTransaction { transactionId: <int> }`.
For OCPP 2.x chargers: the server looks up `ConnectorState.OcppTxId` to retrieve the charger's own string transaction ID, then sends `RequestStopTransaction { transactionId: "<string>" }`. The local-to-OCPP mapping is handled internally — the backend always uses the local integer ID.

**3. Waiting for `StopTransaction`**

Same pattern as remote start: if the charger responds `Accepted`, the server waits up to 60 seconds for `StopTransaction.req` / `TransactionEvent(Ended)`.

**4. Session ends**

When `StopTransaction` arrives:
- `ConnectorState` is cleared: `MeterStartWh`, `MeterValueWh`, `LocalTxId`, `OcppTxId`, `CurrentPowerKw`, `StateOfCharge` all set to `null`
- Publishes `charger.transaction.stopped` with the final meter reading and session start reading (enabling kWh calculation)
- Publishes `charger.remote.stop.response { status: "Accepted" }`

**Failure cases:**
- **Charger responds `Rejected`:** the charger refused to stop (e.g. the transaction ID doesn't match what the charger has active). Published as `{ status: "Rejected" }`. The backend may need to try again or investigate.
- **Timeout:** session is still active but confirmation never arrived. Backend should investigate charger status.

---

## 5. Organic session (charger-initiated)

### What triggers it

A user interacts directly with the charger — taps an RFID card, enters a PIN, or uses the charger's local authorization. The charger decides to start charging without any instruction from the backend. This happens on home chargers with no authorization required, corporate fleet chargers with local RFID lists, or chargers configured for free vend mode.

### Step by step

**1. Charger starts the session on its own**

The charger sends `StartTransaction.req` directly, without any preceding `RemoteStartTransaction` from the server. For OCPP 2.x this is `TransactionEvent(Started)`.

The server assigns a local transaction ID, stores meter start and session state in `ConnectorState`, and immediately publishes `charger.transaction.started`. There is no `charger.remote.start.response` published because no command was issued.

The server responds with the assigned transaction ID so the charger can reference it in future `StopTransaction` messages.

**2. Session runs**

The charger periodically sends `MeterValues` (1.6) or `TransactionEvent(Updated)` (2.x) with energy readings. See [§6 Meter updates](#6-meter-updates-during-a-session).

**3. Charger ends the session**

The user ends the session by tapping their card again, pressing stop on the charger, or unplugging the cable. The charger sends `StopTransaction.req` / `TransactionEvent(Ended)` with the final meter reading and the original transaction ID.

The server clears session state from `ConnectorState` and publishes `charger.transaction.stopped`. If the backend was waiting for a remote-stop confirmation (because it sent `command.remote.stop` that happened to coincide with a user-initiated stop), the pending TCS is completed here.

### Note on transaction IDs

Because no command was issued, the backend learns about the transaction only when `charger.transaction.started` arrives. The backend should store this `transactionId` immediately — it will need it later to issue a `command.remote.stop` if needed, and to reconcile it with `charger.transaction.stopped`.

---

## 6. Meter updates during a session

### What triggers it

During an active charging session chargers periodically send energy and power readings — typically every 60–300 seconds depending on the charger's `MeterValueSampleInterval` configuration. These arrive as:
- `MeterValues.req` in OCPP 1.6 (and some 2.x chargers that send both)
- `TransactionEvent(Updated)` in OCPP 2.x

### Step by step

**1. Charger sends meter readings**

The message contains one or more `sampledValue` entries with different measurands. The server uses `ConnectorState.Adapter.ParseMeterValues()` — the vendor-specific adapter — to extract the values it cares about:
- `Energy.Active.Import.Register` → cumulative energy in Wh (the main meter)
- `SoC` → battery state of charge in % (DC fast chargers only)
- `Power.Active.Import` → instantaneous power in W or kW

**Unit handling:** OCPP 1.6 encodes units as plain strings (`"Wh"`, `"kWh"`); OCPP 2.x encodes them as objects (`{ "name": "Wh" }`). `MeterValueParser` handles both transparently.

**Wallbox External MID:** on Wallbox chargers with an external certified meter, readings are taken upstream of the charger and include the charger's own self-consumption. The `WallboxChargerAdapter` stores this meter type from BootNotification and exposes it via `MeterType`. Future billing logic can use `meterType = "External MID"` to know the readings are from a certified source.

**2. Power calculation**

If the charger reports `Power.Active.Import` directly, that value is used. If not, the server calculates it from two consecutive energy readings:

```
powerKw = (currentWh - previousWh) / (elapsedHours × 1000)
```

Previous reading and timestamp come from `ConnectorState.MeterValueWh` and `ConnectorState.LastMeterValueAt`. Negative or zero deltas (can occur on counter reset or identical readings) are discarded — power is left as `null` in that case.

**3. State written to memory, not DB**

All meter values are written only to `ConnectorState` — no database write occurs on every meter update. This is a deliberate design decision: meter readings are live session data that resets on restart anyway, and avoiding a DB write per update significantly reduces database load during active sessions.

**4. `charger.meter.updated` published**

The server publishes the current energy reading, session start reading, calculated power, and SoC (if available) to RabbitMQ. The backend uses this to update a live session display or progress bar in the app.

The backend can calculate session energy consumed as `meterValueWh - meterStartWh` at any point.

---

## 7. Charger disconnects

### What triggers it

- The charger powers off (planned maintenance, power failure)
- Network connectivity lost (SIM card issue, router reboot)
- The charger firmware crashes and restarts
- The charger is decommissioned

### Step by step

**1. WebSocket closes**

The TCP connection drops. ASP.NET Core detects the WebSocket closing and exits the receive loop in `Program.cs`. The `finally` block runs regardless of how the connection ended.

**2. In-memory state cleared**

`ChargingStationConnections.Remove(stationId)` is called. The `ConnectorState` object for this station — including the WebSocket, all meter readings, session IDs, `CarId`, and any pending TCS — is discarded.

**3. `charger.status.changed` published**

The server publishes `charger.status.changed { status: "Offline", isConnected: false }` via `PushDisconnect()`. This signals the backend that the charger is no longer reachable.

**4. In-flight commands time out**

If the backend had issued a `command.remote.start` or `command.remote.stop` that was still waiting (within its 60-second window), the `TaskCompletionSource` will never be completed. The `WaitAsync(cancellationToken)` call will time out when the `CancellationTokenSource` fires after 60 seconds, and the command handler will publish a `Timeout` response event.

Similarly, if a `charger.authorize.requested` was in flight, the 10-second `CancellationTokenSource` fires and the server rejects — but the charger is already gone so the response goes nowhere.

**5. Reconnection**

When the charger comes back online it starts the [connect and boot flow](#1-charger-connects-and-boots) from scratch. A fresh `ConnectorState` is created with a new `DefaultChargerAdapter` (which is then replaced by the correct vendor adapter on the next BootNotification).

Any session that was active when the charger disconnected is in an unknown state. The charger may resume it and send `StopTransaction` when it reconnects, or it may have already ended internally. The backend should handle `charger.transaction.stopped` arriving well after the disconnect event.

### `Plug.IsOnline` vs `isConnected`

`Plug.IsOnline` in the database is updated by `BootNotification` and `Heartbeat` messages — it represents whether the charger is operationally active. `isConnected` in the RabbitMQ event and REST API is sourced from the live `ConnectorState` and represents whether a WebSocket is currently open.

After a **clean disconnect**: `isConnected` becomes `false` immediately; `Plug.IsOnline` in the DB remains `true` until the next BootNotification updates it (the server doesn't write to DB on a clean close).

After a **watcher-detected silent disconnect**: both `isConnected` and `Plug.IsOnline` are set to `false` — the watcher explicitly marks `IsOnline = false` before aborting the socket, so the DB is consistent even if the charger never reconnects.

---

## 8. Silent disconnect (watcher-detected)

### What triggers it

Some chargers drop the underlying TCP connection without sending a WebSocket Close frame. This can happen due to:
- SIM card session expiry or carrier-side reset
- A NAT or firewall silently dropping the idle connection
- A charger firmware hang that doesn't clean up the socket
- Physical network loss (cable unplugged, router power cycle)

Without intervention the server's `ReceiveAsync` call would hang indefinitely, holding the `ConnectorState` in memory, making the charger look connected when it is not.

### Step by step

**1. Server tracks `LastMessageAt`**

Every time the server receives any OCPP message from a charger — `Heartbeat`, `MeterValues`, `StatusNotification`, anything — it stamps `ConnectorState.LastMessageAt = DateTime.UtcNow`. With a 60-second heartbeat interval, a healthy charger touches this timestamp at most every ~60 seconds.

**2. `ChargerWatcherService` scans periodically**

Every `ChargerWatcher:CheckIntervalSeconds` (default **30 seconds**) the service iterates all entries in `ChargingStationConnections` via `GetAll()`. For each connected station it computes:

```
silentFor = UtcNow - state.LastMessageAt
```

If `silentFor > ChargerWatcher:InactivityThresholdSeconds` (default **180 seconds**), the charger is considered dead. With a 60-second heartbeat interval, this is approximately 3 missed heartbeats.

**3. DB update**

Before aborting the socket, the watcher sets `Plug.IsOnline = false` in the database via a scoped `ChargingDBContext`. This ensures the database reflects reality even if the server restarts before the charger reconnects. All stale chargers in a single scan tick are batched into one `SaveChangesAsync` call.

**4. Socket abort**

`state.Socket.Abort()` is called. This immediately terminates the WebSocket without a clean handshake. The pending `ReceiveAsync` in `Program.cs`'s receive loop throws an exception (or returns an aborted result), which exits the `while` loop.

**5. Standard cleanup via `finally`**

The `finally` block in the WebSocket endpoint runs just as it would for a clean disconnect:
- `ChargingStationConnections.Remove(stationId)` — removes the `ConnectorState`
- `communicator.PushDisconnect(stationId)` — publishes `charger.status.changed { isConnected: false }` to RabbitMQ

Exactly one disconnect event is published. The watcher does not publish anything directly.

**6. Reconnection**

If the charger's network recovers, it opens a new WebSocket connection and goes through the [connect and boot flow](#1-charger-connects-and-boots) as normal.

### Why the watcher only aborts — it doesn't call `Remove()` itself

The watcher could call `ChargingStationConnections.Remove(stationId)` and `PushDisconnect()` directly. It doesn't, because:

1. The `finally` block in Program.cs already does this unconditionally when `ReceiveAsync` exits.
2. Doing it twice would publish two `charger.status.changed` events and potentially cause the backend to double-process the disconnect.
3. Separating concerns keeps the watcher simple: its only job is to detect silence, mark the DB, and abort the socket. Everything else is handled by the existing cleanup path.

### Tuning the threshold

The default 180 seconds = 3 × 60-second heartbeat interval. If you reduce the heartbeat interval (set `BootNotification` response `interval` to a different value), adjust `InactivityThresholdSeconds` proportionally — aim for 3–5× the heartbeat interval to avoid false positives from network hiccups.

```json
"ChargerWatcher": {
  "CheckIntervalSeconds":       30,
  "InactivityThresholdSeconds": 180
}
```
