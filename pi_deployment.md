# Raspberry Pi HTTPS Deployment (LAN)

This setup runs UI + API behind Caddy over HTTPS on your LAN.

## Files added

- `compose.pi-https.yaml`
- `Infra/Caddyfile.pi`
- `UI/Dockerfile`

## Prerequisites

1. Put your cert/key in `Infra/certs`:
   - `Infra/certs/cert.pem`
   - `Infra/certs/key.pem`
2. Make sure your LAN DNS/hosts maps `mealprep.local` to your Pi IP.
3. In API env (`.docker.env`), set:
   - `CORS_ORIGINS=https://mealprep.local`

## Start

```bash
docker compose -f compose.pi-https.yaml up -d --build
```

## Verify

```bash
curl -kI https://mealprep.local
curl -kI https://mealprep.local/api/v1/me
```

Expected:
- UI returns `200`
- `/api/v1/me` returns `401` when not logged in (this is healthy)

## Notes

- UI is served from the `ui` container via Caddy reverse proxy.
- API is internal-only (`expose 5001`) and reachable through `/api/*`.
- If you prefer using `https://192.168.1.98` directly, ensure your certificate includes the IP SAN.
