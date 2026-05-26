# OCPPServer — Database Schema

**Database:** PostgreSQL  
**EF Core provider:** `Npgsql.EntityFrameworkCore.PostgreSQL`  
**Migrations applied automatically** on startup via `db.Database.Migrate()`.

---

## Active Tables

### `Plugs`

One row per physical charging station. Contains **stable metadata only** — information that survives server restarts and is meaningful between sessions.

Live session data (meter readings, current power, state of charge, active transaction IDs) is intentionally **not stored here** — it lives in `ConnectorState` (in-memory) and resets on restart. This eliminates a DB write on every `MeterValues` message.

```sql
CREATE TABLE "Plugs" (
    "Id"               SERIAL          PRIMARY KEY,
    "OcppId"           TEXT            NOT NULL,
    "OcppVersion"      TEXT            NOT NULL  DEFAULT 'ocpp1.6',
    "Status"           INTEGER         NOT NULL  DEFAULT 0,
    "IsOnline"         BOOLEAN         NOT NULL  DEFAULT FALSE,
    "LastStatusUpdate" TIMESTAMPTZ,
    "CreatedAt"        TIMESTAMPTZ     NOT NULL,
    "IsFastCharger"    BOOLEAN         NOT NULL  DEFAULT FALSE,
    "MaxPower"         INTEGER         NOT NULL  DEFAULT 21,
    "Vendor"           TEXT,
    "ChargePointModel" TEXT,
    "ChargePointSN"    TEXT            NOT NULL  DEFAULT '',
    "FirmwareVersion"  TEXT,
    "SIMNr"            TEXT
);
```

#### Column reference

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `integer` | NO | PK, auto-increment |
| `OcppId` | `text` | NO | Charger's OCPP identity — matches the `{stationId}` in `/ws/{stationId}`. Set at WebSocket connect, used as the lookup key everywhere. |
| `OcppVersion` | `text` | NO | Negotiated OCPP sub-protocol: `"ocpp1.6"`, `"ocpp2.0"`, `"ocpp2.0.1"`, `"ocpp2.1"`. Set on first `BootNotification`. |
| `Status` | `integer` | NO | Last known `ChargePointStatus` enum value (0=Available, 1=Preparing, 2=Charging, …). Updated by `StatusNotification`. |
| `IsOnline` | `boolean` | NO | Set to `true` on `BootNotification` / `Heartbeat`. Set to `false` by `ChargerWatcherService` when the charger goes silent. Never set to `false` on a clean disconnect — remains `true` until the next Heartbeat cycle. |
| `LastStatusUpdate` | `timestamptz` | YES | UTC timestamp of the last DB write for this row. Updated on `BootNotification`, `Heartbeat`, `StatusNotification`. |
| `CreatedAt` | `timestamptz` | NO | UTC timestamp when this row was first inserted (first time the charger connected). |
| `IsFastCharger` | `boolean` | NO | Admin-set flag. `true` = DC fast charger (supports ISO 15118, SoC, high-power). Used by the platform to show DC-specific features. |
| `MaxPower` | `integer` | NO | Admin-set maximum power in **kW** (e.g. `22` = 22 kW AC, `150` = 150 kW DC). Exposed in the REST API for display and capacity checks. Default `21` (21 kW — typical single-phase AC charger). |
| `Vendor` | `text` | YES | Charger vendor string from `BootNotification.chargePointVendor` / `chargingStation.vendorName`. Used to select the `IChargerAdapter`. |
| `ChargePointModel` | `text` | YES | Model string from `BootNotification`. |
| `ChargePointSN` | `text` | NO | Hardware serial number from `BootNotification.chargePointSerialNumber` / `chargingStation.serialNumber`. |
| `FirmwareVersion` | `text` | YES | Firmware version string from `BootNotification`. |
| `SIMNr` | `text` | YES | SIM card ICCID from `BootNotification.iccid` (modem field in OCPP 2.x). |

#### `Status` enum values

| Integer | Name | Meaning |
|---|---|---|
| 0 | `Available` | Connector is free, ready to charge |
| 1 | `Preparing` | Cable plugged in, waiting for authorization |
| 2 | `Charging` | Active charging session |
| 3 | `SuspendedEVSE` | Paused by the charger (e.g. scheduled charging) |
| 4 | `SuspendedEV` | Paused by the vehicle (e.g. battery management) |
| 5 | `Finishing` | Session ending (cable still plugged) |
| 6 | `Reserved` | Reserved for a specific user |
| 7 | `Unavailable` | Out of service (admin disabled or firmware update) |
| 8 | `Faulted` | Hardware or communication fault |

*OCPP 2.x uses a different but overlapping status vocabulary (`Occupied` instead of `Preparing`, etc.). The server maps 2.x statuses to this enum before writing to the DB.*

#### Indexes

| Index | Columns | Type | Notes |
|---|---|---|---|
| `PK_Plugs` | `Id` | Primary key | Auto-created by EF |
| `IX_Plugs_OcppId` | `OcppId` | Unique | Eliminates sequential scans on every OCPP message lookup |

---

### `ErrorLogs`

Structured trace records persisted by `TracingService`. Only entries at or above `Tracing:MinPersistLevel` (default `Warning`) are written here. DB writes are fire-and-forget — callers are never blocked.

Old rows are deleted automatically by `ErrorLogCleanupService` once per day at `Tracing:CleanupHourUtc` (default 03:00 UTC). Retention window is controlled by `Tracing:RetentionDays` (default 3 days).

```sql
CREATE TABLE "ErrorLogs" (
    "Id"            SERIAL          PRIMARY KEY,
    "ChargePointId" VARCHAR(100),
    "SessionId"     INTEGER,
    "OccurredAt"    TIMESTAMPTZ     NOT NULL,
    "Level"         INTEGER         NOT NULL,
    "Source"        VARCHAR(100)    NOT NULL,
    "Message"       TEXT            NOT NULL,
    "IsSolved"      BOOLEAN         NOT NULL  DEFAULT FALSE,
    "SolvedAt"      TIMESTAMPTZ
);
```

#### Column reference

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `integer` | NO | PK, auto-increment |
| `ChargePointId` | `varchar(100)` | YES | `OcppId` of the affected charger, when known |
| `SessionId` | `integer` | YES | `LocalTxId` of the active session, when known |
| `OccurredAt` | `timestamptz` | NO | UTC timestamp set at the moment of the trace call |
| `Level` | `integer` | NO | `ErrorLogLevel` enum: 0=Info, 1=Warning, 2=Error, 3=Critical |
| `Source` | `varchar(100)` | NO | Short tag identifying the subsystem (e.g. `"Watcher"`, `"OCPP"`, `"RemoteStart"`) |
| `Message` | `text` | NO | Human-readable description. For `Exception` level includes type, message, and full stack trace. |
| `IsSolved` | `boolean` | NO | Admin-settable resolved flag. Default `false`. |
| `SolvedAt` | `timestamptz` | YES | UTC timestamp set when an admin marks the entry as solved. |

#### `Level` enum values

| Integer | Name | Written by |
|---|---|---|
| 0 | `Info` | Lifecycle events (connect, session started, etc.) |
| 1 | `Warning` | Unexpected-but-recoverable situations (unknown status, missing field, timeout) |
| 2 | `Error` | Problems affecting a specific charger or session |
| 3 | `Critical` | Unhandled exceptions — includes full stack trace |

#### Configuration (`appsettings.json`)

```json
"Tracing": {
  "MinPersistLevel": "Warning",
  "RetentionDays":   3,
  "CleanupHourUtc":  3
}
```

---

## Columns intentionally absent from `Plugs`

These columns existed in earlier versions of the schema and have been removed. They are documented here to explain the design decision.

| Column | Removed in migration | Reason |
|---|---|---|
| `MeterValue` | `RemoveLiveMeterColumns` | Live cumulative energy reading (Wh). Resets on server restart anyway; no value persisting it. Moved to `ConnectorState.MeterValueWh` (in-memory). |
| `LastMeterValueAt` | `RemoveLiveMeterColumns` | Timestamp of last meter reading. Only used for power calculation within a session; meaningless after restart. Moved to `ConnectorState.LastMeterValueAt`. |
| `StateOfCharge` | `RemoveLiveMeterColumns` | Battery SoC %. DC charger sessions only; transient. Moved to `ConnectorState.StateOfCharge`. |
| `MeterStart` | `RemoveMeterStartStopFromConnector` (on `Connectors`) | Session start reading. Persisted in `EVChargingDB.ChargingSessions` via RabbitMQ. Held in-memory during the session in `ConnectorState.MeterStartWh`. |
| `MeterStop` | `RemoveMeterStartStopFromConnector` (on `Connectors`) | Session end reading. Written to `EVChargingDB.ChargingSessions` directly at `StopTransaction`. |
| `ActiveTransactionId` | `MigrateToPlugs` (dropped with `Connectors`) | OCPP transaction ID. Persisting it was fragile across restarts. Now derived from in-memory `ConnectorState.LocalTxId` / `.OcppTxId`. |
| `SessionStartedAt` | `MigrateToPlugs` (dropped with `Connectors`) | Session start time. Belongs in `EVChargingDB.ChargingSessions`. |
| `CurrentPowerKw` | `MigrateToPlugs` (dropped with `Connectors`) | Instantaneous power. Purely live data; moved to `ConnectorState.CurrentPowerKw`. |

---

## Legacy / Dropped Tables

### `Connectors` (dropped)

The original table. Dropped in migration `MigrateToPlugs` (`20260525`) when the schema was simplified to separate stable metadata from live session state. The `Connector.cs` model class still exists in the codebase as a reference but is **not registered** in `ChargingDBContext` and has no corresponding table.

The `Plugs` table was created as its replacement at the same time.

---

## Migration history

| Migration | Timestamp | What changed |
|---|---|---|
| `InitialCreate` | 20260518-215610 | Created `Connectors` table |
| `AddChargerInfoFields` | 20260518-223934 | Added vendor/model/SN/firmware fields to `Connectors` |
| `AddChargerTypeAndVisibility` | 20260518-224400 | Added `IsFastCharger`, `ShowOnMap`, `Name`, `Address`, `Latitude`, `Longitude` to `Connectors` |
| `AddNumberOfConnectors` | 20260518-224621 | Added `NumberOfConnectors` to `Connectors` |
| `AddMeterStartStop` | 20260519-120000 | Added `MeterStart`, `MeterStop` to `Connectors` |
| `AddSessionStartedAt` | 20260520-153024 | Added `SessionStartedAt` to `Connectors` |
| `AddCurrentPowerKw` | 20260520-154105 | Added `CurrentPowerKw` to `Connectors` |
| `RemoveOrphanTables` | 20260520-185609 | Dropped unused tables |
| `RemoveMeterStartStopFromConnector` | 20260522-140000 | Dropped `MeterStart`, `MeterStop` — moved to `EVChargingDB` |
| `MigrateToPlugs` | 20260525-151613 | Dropped `Connectors`; created `Plugs` (clean schema, no live fields) |
| `AddStateOfCharge` | 20260525-161648 | Added `StateOfCharge` to `Plugs` *(immediately superseded — see next row)* |
| `RemoveLiveMeterColumns` | 20260525-204925 | **Dropped** `MeterValue`, `LastMeterValueAt`, `StateOfCharge` from `Plugs` — all live session data now lives exclusively in `ConnectorState` |
| `AddErrorLogs` | 20260526-085136 | Created `ErrorLogs` table — structured tracing with DB persistence |
| `AddPlugOcppIdIndex` | 20260526-092919 | Added unique index `IX_Plugs_OcppId` on `Plugs.OcppId` |

---

## What lives where

| Data | Storage | Lifetime |
|---|---|---|
| Charger hardware identity (vendor, model, SN, firmware) | `Plugs` DB | Permanent; updated on BootNotification |
| Last known OCPP status | `Plugs.Status` | Permanent; updated on StatusNotification |
| Online/offline flag | `Plugs.IsOnline` | Permanent; updated on Heartbeat / Watcher |
| Admin metadata (IsFastCharger, MaxPower) | `Plugs` DB | Permanent; set via admin REST API |
| Open WebSocket | `ConnectorState.Socket` | Session (lost on restart) |
| Live meter reading (Wh) | `ConnectorState.MeterValueWh` | Session |
| Session start reading (Wh) | `ConnectorState.MeterStartWh` | Session |
| Instantaneous power (kW) | `ConnectorState.CurrentPowerKw` | Session |
| Battery SoC (%) | `ConnectorState.StateOfCharge` | Session |
| Active transaction IDs | `ConnectorState.LocalTxId` / `.OcppTxId` | Session |
| Vehicle EVCC ID | `ConnectorState.CarId` | Session (cleared on Available) |
| Session start reading (billing) | `EVChargingDB.ChargingSessions` | Permanent (other service) |
| Session end reading (billing) | `EVChargingDB.ChargingSessions` | Permanent (other service) |
| Structured trace / error logs | `ErrorLogs` DB | Rolling (auto-purged after `RetentionDays`) |
