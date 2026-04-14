# Deployment Guide

This project supports multiple deployment paths depending on environment and hardware.

## Option 1: Self-Hosted HTTP (LAN)

Use `compose.http.yaml` when you want a simple LAN deployment without certificates.

```bash
docker compose -f compose.http.yaml up -d --build
```

Notes:

- Default port mapping is `80:80` on the Caddy service.
- Default API environment is `ASPNETCORE_ENVIRONMENT=Development` in this HTTP profile.
- Default CORS behavior requires you to set `CORS_ORIGINS` to the exact browser origin (for example `http://mealprep.local` or `http://<host-ip>`).
- Default MCP routing is `/mcp/*` proxied to the API via `Infra/Caddyfile.http`.
- These defaults are configurable; see [Customize these defaults](#customize-these-defaults).

## Option 2: Self-Hosted HTTPS (LAN)

Use `compose.https.yaml` when you have local certs and want encrypted traffic.

Required files:

- `Infra/certs/cert.pem`
- `Infra/certs/key.pem`

Run:

```bash
docker compose -f compose.https.yaml up -d --build
```

Notes:

- Caddy terminates TLS on port `443`.
- Keep `CORS_ORIGINS` aligned with your HTTPS origin (for example `https://mealprep.local`).
- MCP is proxied on `/mcp/*` and should use HTTPS in clients.

## Option 3: Generic Docker Host

For local server, VM, or NAS deployment:

1. Create `.docker.env` with production-ready values.
2. Run:

```bash
docker compose up -d --build
```

This uses `compose.yaml` and exposes API on `5001` and supporting services on their default ports.

## Option 4: Build/Run From Source

Use this when you want direct app execution outside containers.

```bash
pnpm install
dotnet build
dotnet run --project Api
pnpm dev
```

Notes:

- You still need backing services (Postgres, Redis, MinIO), usually via `docker compose up -d`.
- Source-based runs are better for development/debugging than production operations.

## ARM Image Build Strategy

To avoid building on low-power hardware, build and push ARM images from a stronger machine:

```bash
REGISTRY=ghcr.io/<org-or-user> IMAGE_TAG=<tag> ./scripts/build-pi-images.sh
```

Optional env vars for that script:

- `PLATFORMS` (e.g. `linux/arm64` or multi-arch)
- `PUSH_LATEST=true`
- `IMAGE_NAMESPACE=<name>`

## Customize these defaults

- **Ports:** edit `ports` mappings in `compose.http.yaml` or `compose.https.yaml`.
- **API env vars:** edit `environment` in compose and runtime values in `.docker.env`.
- **CORS:** set `CORS_ORIGINS` in `.docker.env` to your deployment origin(s).
- **Routing:** edit `Infra/Caddyfile.http` and `Infra/Caddyfile` for `api`/`mcp` path behavior.
