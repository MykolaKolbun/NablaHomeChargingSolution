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

`StationDto`: `{id, ocppId, name, role: "Owner"|"Member", claimedAt}`

Claim codes: format `XXXX-XXXX`, case and dashes ignored, **one-time**. Unknown station, wrong code
and already-claimed return the same `400` (no station-ID discovery). Rate limit: 5 req/min per client IP.

## Admin (`X-Admin-Key`)

| Method | Path | Body | Response |
|---|---|---|---|
| POST | `/api/admin/stations` | `{ocppId, name}` | `200 {id, ocppId, name, claimCode}` · `409` exists |
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
