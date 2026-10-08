# Nabla Home — Deployment

Target: the shared Raspberry Pi 4 (ARM64, ~1 GB RAM) that also hosts other demo projects.
Same scheme as the commercial solution: **GitHub Actions builds an ARM64 image → GHCR →
SSH to the Pi through Cloudflare Tunnel → `docker compose pull && up -d`.**

## Layout on the Pi

```
~/NablaHome/
├── docker-compose.yml   ← copied by CI from deploy/docker-compose.yml
└── .env                 ← secrets, upserted by CI (chmod 600)
```

Compose project name `nablahome` → containers `nablahome-db-1`, `nablahome-rabbitmq-1`,
`nablahome-evocpp-1`. Own network and volumes; nothing shared with other projects.

| Service | Host port | Memory limit | Notes |
|---|---|---|---|
| `db` (postgres:16-alpine) | `127.0.0.1:5434` | 128 MB | DBs: `nablahome` (EVHomeAPI), `evocpp` (created by EF on first start) |
| `rabbitmq` | `127.0.0.1:15673` (UI) | 192 MB | vhost `/home`, AMQP not exposed to host |
| `evocpp` | `8091` | 160 MB | OCPP WebSocket + admin REST |
| `home-api` (EVHomeAPI) | `127.0.0.1:8090` | 160 MB | localhost only — reached via cloudflared; see `docs/api.md` |

## Public hostnames (Cloudflare Tunnel)

Add to `/etc/cloudflared/config.yml` on the Pi **above** the final `http_status:404` rule:

```yaml
  - hostname: home-ocpp.alternatiview.com.ua
    service: http://localhost:8091
  - hostname: home-api.alternatiview.com.ua
    service: http://localhost:8090
```

Then:

```bash
cloudflared tunnel route dns <tunnel-name-or-id> home-ocpp.alternatiview.com.ua
cloudflared tunnel route dns <tunnel-name-or-id> home-api.alternatiview.com.ua
sudo systemctl restart cloudflared
```

Charger OCPP URL: `wss://home-ocpp.alternatiview.com.ua/ws/{chargePointId}`

## GitHub repository secrets

| Secret | Value |
|---|---|
| `PI_HOST` | Cloudflare SSH hostname of the Pi (same as commercial repos) |
| `PI_USER` | `sorrow` |
| `PI_SSH_PRIVATE_KEY` | Base64 ed25519 key authorised on the Pi (same as commercial repos) |
| `HOME_DB_PASSWORD` | New random password |
| `HOME_RABBITMQ_PASSWORD` | New random password |
| `HOME_TRACE_DOWNLOAD_KEY` | New random secret for `/api/admin/traces` |
| `HOME_JWT_KEY` | Random, ≥ 32 chars — signs user JWTs. Changing it logs everyone out |
| `HOME_ADMIN_KEY` | Random, ≥ 24 chars — header `X-Admin-Key` for `/api/admin/*` |

Generate on Windows (PowerShell):

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(36))
```

> `HOME_DB_PASSWORD` / `HOME_RABBITMQ_PASSWORD` are applied only when the volumes are first
> created. Changing them later requires changing them inside Postgres/RabbitMQ too.

## Workflows

| Workflow | Trigger | Does |
|---|---|---|
| `.github/workflows/evocpp.yml` | push to `master` touching `EVOCPP/OCPPServer/**`, `deploy/**`; manual | build `ghcr.io/mykolakolbun/nablahome-evocpp`, upload compose + secrets, `up -d evocpp` (starts db + rabbitmq as dependencies) |
| `.github/workflows/evhomeapi.yml` | push to `master` touching `EVHomeAPI/**`, `EVHomeAPI.Tests/**`, `deploy/**`; manual | run tests → build `ghcr.io/mykolakolbun/nablahome-evhomeapi`, upload compose + secrets, `up -d home-api`, check `/api/health` |

## Useful commands on the Pi

```bash
cd ~/NablaHome
docker compose ps
docker compose logs evocpp --tail 50 -f
docker stats --no-stream $(docker compose ps -q)
```
