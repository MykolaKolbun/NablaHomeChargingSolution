# OCPPServer — Known Gaps & Deferred Work

Tracked items that are incomplete, incorrect, or deferred. Each entry has a severity tag:

- 🔴 **Bug** — incorrect behaviour in production
- 🟠 **Missing** — feature is designed/specified but not implemented
- 🟡 **Debt** — works, but messy or fragile; should be cleaned up
- 🔵 **Perf** — works correctly, but could be faster at scale

---

## Correctness / Bugs

### 🔴 `Plug.Status` serialises as integer in REST responses

**File:** `Program.cs` — `MapGet /api/admin/plugs` and `MapGet /api/admin/plugs/{ocppId}`

No `JsonStringEnumConverter` is registered on the builder, so `p.Status` (a `ChargePointStatus` enum) serialises as its numeric value (`2`) instead of its name (`"Charging"`). The messaging spec (§5.1) documents the field as a string.

**Fix:** Either add a `.Select(...)` projection that calls `.ToString()` on the status (consistent with what `error-logs` already does for `Level`), or register the converter globally:
```csharp
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```

---

### 🔴 `README.md` documents stale API routes

**File:** `README.md`

The README still references the old `Connector`-era routes:
- `GET /api/admin/chargers` → current route is `GET /api/admin/plugs`
- `PUT /api/admin/chargers/{ocppId}` → current route is `PUT /api/admin/plugs/{ocppId}`

The PUT body in the README lists fields that no longer exist on the `Plug` model: `name`, `address`, `latitude`, `longitude`, `showOnMap`, `maxPowerKw`, `numberOfConnectors`. These were on `Connector` and were dropped with the `MigrateToPlugs` migration.

**Fix:** Update `README.md` to reflect the current `Plugs` API.

---

## Missing Functionality

### 🟠 No endpoint to mark an ErrorLog as solved

**File:** `Program.cs`

`ErrorLog` has `IsSolved` and `SolvedAt` fields for the admin panel to mark issues as resolved, but there is no REST endpoint to set them. The `GET /api/admin/error-logs` endpoint is in place; write is not.

**Fix:** Add:
```
PATCH /api/admin/error-logs/{id}/solve
```
Sets `IsSolved = true`, `SolvedAt = DateTime.UtcNow`.

---

### ~~`BootNotification` always responds `Accepted` — no charger allowlist~~ *(by design)*

**Decision:** The OCPP server accepts every connecting charger unconditionally. Allowlist / access control is the responsibility of the EVChargingApi application backend. The server's job is transport and protocol handling only.

---

### 🟠 `ChargerSim` project has no implementation *(deferred)*

**Project:** `ChargerSim/`

The project is a Docker scaffold stub only. Running it does nothing. Planned: a minimal simulator that connects via WebSocket and drives the full OCPP 1.6 session lifecycle (BootNotification → Heartbeat → Authorize → StartTransaction → MeterValues → StopTransaction) to allow integration testing without a physical charger.

---

## Technical Debt

### 🟡 `Connector.cs` model is dead code

**File:** `DataBase/DBModels/Connector.cs`

The `Connectors` table was dropped in migration `MigrateToPlugs`. `ChargingDBContext` has no `DbSet<Connector>`. The model class still exists in the codebase and carries a stale TODO comment. It is never instantiated at runtime.

**Fix:** Delete `Connector.cs`. The migration history documents what was in it.

---

### 🟡 Old WebSocket handler is dead commented-out code in `Program.cs`

**File:** `Program.cs` lines 114–161

The original `/ws/{stationId}` handler (before OCPP version negotiation was added) is commented out and takes up ~50 lines. The comment above it says "Uncomment when new version not working" — it is a fallback that has not been needed.

**Fix:** Delete the commented block. Git history preserves it if ever needed.

---

### 🟡 Old REST session-control endpoints are still scaffolded in `Program.cs`

**File:** `Program.cs` lines 258–335 — `//TODO: Remove REST API...`

The old `/api/chargers/{id}/start-session`, `/stop-session`, `/status`, and `/meter` endpoints are commented out with a TODO to remove them once the RabbitMQ-based flow is stable. They have been superseded by `command.remote.start` / `command.remote.stop`.

**Fix:** Delete the commented REST blocks and the `//TODO` note.

---

### ~~`Plug.MaxPower` default is `21` (meaningless value)~~ *(by design)*

**Decision:** The field stores kW (not watts). `21` = 21 kW — a standard single-phase AC wall charger. The default is intentional and is updated per charger in the admin console.

---

## Performance

### 🔵 No index on `Plugs.OcppId`

**File:** `Data/ChargingDBContext.cs` / EF migration

Every `BootNotification`, `Heartbeat`, `StatusNotification`, `StartTransaction`, `StopTransaction`, and `ChargerWatcherService` DB write performs a `WHERE "OcppId" = @id` query. There is no index on this column — PostgreSQL does a sequential scan.

At low charger counts (< 50) this is fine. For larger deployments it degrades.

**Fix:** Add a migration with a unique index:
```csharp
modelBuilder.Entity<Plug>()
    .HasIndex(p => p.OcppId)
    .IsUnique();
```

---

## Summary Table

| # | Severity | Area | One-liner |
|---|---|---|---|
| 1 | ✅ Fixed | REST API | `Plug.Status` serialises as int, not name string |
| 2 | ✅ Fixed | Docs | README documents deleted routes and request body fields |
| 3 | ✅ Fixed | REST API | No `PATCH /error-logs/{id}/solve` endpoint |
| 4 | 🚫 By design | OCPP | `BootNotification` always `Accepted` — access control is EVChargingApi's responsibility |
| 5 | 🟠 Deferred | Testing | `ChargerSim` project is an empty stub — full simulator planned |
| 6 | ✅ Fixed | Codebase | `Connector.cs` model is dead code |
| 7 | ✅ Fixed | Codebase | Old WebSocket fallback handler is dead commented code |
| 8 | ✅ Fixed | Codebase | Old REST session endpoints are dead commented code |
| 9 | 🚫 By design | DB model | `Plug.MaxPower` default `21` = 21 kW (standard AC charger); unit is kW |
| 10 | ✅ Fixed | DB | No index on `Plugs.OcppId` — sequential scan on every OCPP message |
