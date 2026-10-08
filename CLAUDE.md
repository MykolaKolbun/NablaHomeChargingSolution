# Engineering Rules

## Execution Model

For every task:

1. Create a short plan
2. Break work into small sequential steps
3. Verify each step before continuing
4. Report verification results
5. Avoid broad refactors unless requested

Execution loop:

PLAN → IMPLEMENT → VERIFY → CONTINUE

## Verification Requirements

After every code change:

- run relevant tests
- run type checks
- run linting if applicable
- confirm expected behavior

Never claim success without verification.

## Code Modification Rules

- Read related files before editing
- Do not assume APIs exist
- Preserve backward compatibility
- Prefer minimal localized changes

## Failure Handling

If verification fails:
- stop
- explain root cause
- propose minimal fix
- re-run verification
# Project Context

Nabla Home — home EV charger management (no payments, no fiscal, no wallet).
Fully separate from the commercial solution in `..\NablaPublicChargingSolution`.

- `EVOCPP/` — OCPP gateway, home version (imported from commercial OCPP_RD with history, now independent)
- `HomeApi/` — .NET 10 backend (system of record for home users, stations, sessions)
- `HomeApi.Tests/` — xUnit tests
- `HomeApp/` — Expo / React Native app (design mirrors `NablaPublicChargingSolution/EVChargingApp`)
- `deploy/` — docker-compose: home-api, evocpp, postgres, rabbitmq
- `docs/` — architecture and decisions (read `docs/architecture.md` first)

Rules specific to this repo:
- `..\NablaPublicChargingSolution` is a **read-only reference**. Read it for patterns and
  ideas; never edit, commit to, build images from, or depend on anything there.
  No shared code, packages, images, DBs or brokers. Anything reused is copied into this repo.
- Improvements found here that would also benefit the commercial solution are NOT applied
  there from this repo. Log them in `docs/commercial-backlog.md` (what, why, where it lives
  here); they are applied later, separately, in the commercial repos.
- `EVOCPP/` is our own gateway. Change it freely; no backward-compatibility obligation
  to the commercial OCPP_RD and no syncing in either direction.
- Never copy wallet / payment / fiscal code from EVChargingApi.
- Target chargers: OCPP 1.6J first.
