# Supernova on the commercial OCPP server — log watching runbook

For a Claude Code session in `NablaPublicChargingSolution` that tests the commercial stack (OCPP_RD + EVChargingApi)
with the real Wallbox Supernova. Written from the Nabla Home session, 2026-11-06.

## The setup

| What | Value |
|---|---|
| Charger | Wallbox Supernova 60 kW DC, SN 669850, fw 4.6.16, OCPP 1.6J only |
| OCPP id | `wbx669850` — **one** charge point, connectors `connectorId` 1 and 2 (not `#1`/`#2`) |
| Connected to | commercial OCPP `wss://ocpp.alternatiview.com.ua/ws` → container `evchargingapi-ocpp-1` on the Pi |
| Power | 5 of 6 power modules (QPM) work → `Power.Offered` ≈ 49–50 kW; a 6th, flaky donor module may be in slot 5 |
| Charger config | `AutoChargeEnabled = true`, `AuthorizationCacheEnabled = true`, `LocalPreAuthorize = true`, `AuthorizeRemoteTxRequests = false` |
| Test car | Tesla, Autocharge idTag `VID:98ed5cdc5016` (EVCCID) |
| Background | `docs/hardware/supernova-669850.md` (all timelines, error codes), `docs/commercial-backlog.md` CB-008…CB-012 |

The charger belongs to a client. **After testing it must go back**: `ConnectionUrl` =
`wss://ocpp.1up.energy/websocket/CentralSystemService/`, `AutoChargeEnabled = false`, no charging profiles left.

## Access to the Pi

From outside the LAN, SSH goes through Cloudflare Access (cloudflared is installed on the PC):

```bash
CF="/c/Program Files (x86)/cloudflared/cloudflared.exe"
ssh -o ProxyCommand="\"$CF\" access ssh --hostname %h" -o ConnectTimeout=30 sorrow@ssh.alternatiview.com.ua '<command>'
```

From the home LAN: `ssh sorrow@10.10.51.105`. `sudo` needs a password — never type it; ask the user.

**Memory is the hard limit (905 MB RAM + 904 MB swap).** Today the Pi hung and rebooted repeatedly with all stacks
running. Now only the commercial stack (`evchargingapi-*`, 6 containers, ≈ 350 MB) and Nabla Home (`nablahome-*`, 4)
run; SilverBreeze, `mobilepaymentweb`, `indectlocalcap` are stopped with `restart=no` — do not start them during tests.
Check `uptime; free -m` before heavy actions. Never stop `evchargingapi-db-1` (shared with other projects).
Multi-line remote scripts: pipe a local file into `ssh … 'bash -s' < script.sh` (no quoting hell).

## Watching the commercial OCPP log

Raw log (one line per message, continuation lines start with whitespace):

```bash
docker logs --since 10m evchargingapi-ocpp-1 2>&1 | grep -v -E '^\s|Watcher|Heartbeat' | grep -i wbx669850 | tail -40
```

Live, compact stream — save as `watch-public.sh`, run with `ssh … 'bash -s' < watch-public.sh`
(in Claude Code: the `Monitor` tool, so each line becomes an event):

```bash
docker logs -f --since 0s evchargingapi-ocpp-1 2>&1 \
 | grep --line-buffered -v -E '^\s' \
 | grep --line-buffered -i -E 'wbx669850|RemoteStart|RemoteStop|authorize|Exception|ERR ' \
 | grep --line-buffered -v -E 'Heartbeat|Watcher' \
 | awk '
 {
   t = ""; if (match($0, /[0-9][0-9]:[0-9][0-9]:[0-9][0-9]/)) t = substr($0, RSTART, RLENGTH)
   line = $0; sub(/^[0-9.]+ [0-9:]+ /, "", line)
   if (line ~ /"MeterValues"/) {
     out = ""
     n = split("Current.Import Energy.Active.Import.Register Power.Active.Import Power.Offered SoC", ms, " ")
     for (i = 1; i <= n; i++) {
       pat = "\"measurand\": \"" ms[i] "\",\"unit\": \"[^\"]*\",\"value\": \"[^\"]*\""
       if (match(line, pat)) { s = substr(line, RSTART, RLENGTH); sub(/.*"value": "/, "", s); sub(/"$/, "", s); out = out " " ms[i] "=" s }
     }
     c = ""; if (match(line, /"connectorId": [0-9]/)) c = " c" substr(line, RSTART + 15, 1)
     print t " MV" c out; fflush(); next
   }
   if (line ~ /\[← IN \]/ && line ~ /"StatusNotification"/) {
     v = ""; if (match(line, /"vendorErrorCode": "[^"]+"/)) v = substr(line, RSTART + 20, RLENGTH - 21)
     if (v != "") { print t " ERR " v; fflush() }
     next
   }
   if (line ~ /\[← IN \]/ && line ~ /"(StartTransaction|StopTransaction|Authorize)"/) { s = line; sub(/.*\[2,"[0-9]+",/, "", s); print t " " substr(s, 1, 200); fflush(); next }
   if (line ~ /\[← IN \]|\[→ OUT\]|\[← CALL\]/) next
   print t " " substr(line, 1, 200); fflush()
 }'
```

The Monitor tool expires after 30 min — re-arm it. If the SSH tunnel drops (Pi overloaded), the stream ends:
check `uptime; free -m` and re-arm.

API side: `docker logs --since 10m evchargingapi-api-1 2>&1 | grep -i -E 'wbx669850|session|authoriz|wallet' | tail`.

## What the log looks like (real Supernova sequence)

```
Preparing (connector 2)
Authorize {"idTag":"VID:98ed5cdc5016"}        ← ~6 s after Preparing; ABSENT when the VID is in the charger's cache
StartTransaction {connectorId: 2, meterStart: …, idTag: VID:…}   ← ~25–35 s after Preparing (isolation test)
MeterValues every 60 s: Current.Import, Energy.Active.Import.Register (Wh), Power.Active.Import (W),
                        Power.Offered (W, ≈ 49 000 with 5 modules), SoC (%), Voltage
StopTransaction {reason: Local | Remote | Other, transactionId} ← no connectorId
```

Notes:
- Connector 1 and 2 have **separate meter registers** (one dual-channel Acrel meter): connector 2 ≈ 71 016 000 Wh,
  connector 1 ≈ 19 114 000 Wh. Energy of a session = meterStop − meterStart of the same connector.
- After every boot the charger's timestamps are `2025-02-13…` until it syncs — use server time.
- Faults arrive as `StatusNotification` with `errorCode = OtherError` and the Wallbox code in `vendorErrorCode`
  (`DCF:<block>:<nnn>:<type>:<code>`, text in `info`). Normal on power-off: `PSY:001:HWR`, `CFN:00x:MES:BLK`, `PMA:001:COM`.
  Module faults to stop on: `PMO:00x:HWR:OD2/OF2`, `PMO:00x:OVT:BAT/UVT:BAT`; **`ISM:00x:SAF:000` (isolation) → stop the test.**

## What to check (known gaps — see commercial-backlog.md)

| # | Scenario | Expected on today's commercial code |
|---|---|---|
| PR-003 / CB-008 #7, gap B | Plug in the Tesla (VID cached) | `StartTransaction` with `VID:` **without** `Authorize` → charging starts, does EVChargingApi record a session / wallet check? |
| PR-003 gap A | `Authorize` VID with `AutoStart:Iso15118Enabled=false` | API answers `Rejected` fast; charger shows auth error / stays Preparing |
| CB-010 | Authorize when the backend answers fast | OCPP_RD may log "backend timeout — rejecting vehicle" although the API answered (publish-before-register race) |
| CB-011 | Two cars, connectors 1 and 2 at once | one state per ocppId: meters / MeterStartWh / SoC of the two connectors get mixed |
| CB-012 | Charger reconnects before the old socket closed | commands discarded as "not connected", false Offline |
| Power | `Power.Offered` over a few minutes | ≈ 49 kW (5 modules); a drop 59 → 49 kW = the 6th module dropped out |

To ask the charger something without the API, the commercial OCPP_RD has its own command paths (RabbitMQ
`command.*` / REST) — check `OCPP_RD/docs/messaging-api-spec.md` there; Nabla Home's `command.dev.call` does not exist
in OCPP_RD.

## Never

- Edit Nabla Home from the commercial session (and vice versa) — each repo applies its own changes.
- Print secrets (`.env`, compose env, connection strings, RabbitMQ passwords).
- Write to the commercial production DB without the user's explicit OK (DB role `evuser`, DB `evcharging`).
- Start the stopped stacks or change `mem_limit`s without asking.
