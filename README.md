# Meal Prep

Meal Prep is a web app for saving recipes, importing recipes from the web, and turning ingredients into shopping lists.

## Projects

| Project  | Description                              | Link                                       |
| -------- | ---------------------------------------- | ------------------------------------------ |
| Api      | ASP.NET Core minimal API backend         | [Api/README.md](./Api/README.md)           |
| UI       | Vite + React + TypeScript frontend       | [UI/README.md](./UI/README.md)             |
| E2eTests | Playwright browser end-to-end test suite | [E2eTests/README.md](./E2eTests/README.md) |

## Quick Start

From the repo root:

```bash
pnpm install
docker compose up -d
dotnet build
dotnet run --project Api
```

In another shell:

```bash
pnpm dev
```

Or run the API through the monorepo wrapper:

```bash
pnpm api:dev
```

This starts the local stack for:

- PostgreSQL
- Redis
- MinIO
- OpenTelemetry Collector

## Product Direction

The current platform already includes:

- User authentication
- Multi-workspace support
- Settings and account management
- Shared local infrastructure
- End-to-end browser testing

The intended product workflow is:

- Save recipes manually
- Pull recipes from web pages
- Extract and organize ingredients
- Build shopping lists from selected recipes

## Backend Test Dependencies

Endpoint and integration tests in `Api.Tests` expect backend dependencies to be available:

- PostgreSQL on `127.0.0.1:5432`
- Redis on `127.0.0.1:6379`
- MinIO on `127.0.0.1:9000`

Start them with:

```bash
docker compose up -d
```

The test suite reads environment variables from `.env.test` at the repo root. No other env or config sources are used for tests.

## Test Container

Run a dedicated test container:

```bash
docker compose -f compose.yaml -f compose.test.yaml up -d
docker compose -f compose.yaml -f compose.test.yaml exec tests bash
```

Then inside the container:

```bash
dotnet test Api.Tests/Api.Tests.csproj
```

## Turbo

This repo uses Turborepo to run workspace commands from the repo root.

Common root commands:

```bash
pnpm build
pnpm lint
pnpm typecheck
pnpm api:build
pnpm api:dev
pnpm api:run
pnpm api:test
pnpm test:e2e
pnpm install:browsers
```

Target a single workspace directly when needed:

```bash
pnpm --filter meal-prep-api build
pnpm --filter meal-prep-api run
pnpm --filter meal-prep-api-tests test
pnpm --filter meal-prep-ui build
pnpm --filter meal-prep-infra railway:status
pnpm --filter meal-prep-e2e-tests test
```

The main workspace package names are:

- `meal-prep-api`
- `meal-prep-api-tests`
- `meal-prep-ui`
- `meal-prep-infra`
- `meal-prep-e2e-tests`

## Infrastructure

- Database: PostgreSQL
- Cache: Redis
- Storage: MinIO
- Observability: OpenTelemetry
- CI/CD: GitHub Actions

## Raspberry Pi Deploy Profiles

Two Pi-oriented compose profiles are available at the repo root:

- `compose.pi-https.yaml`: HTTPS LAN deployment with Caddy TLS termination.
- `compose.pi-http.yaml`: HTTP-only LAN deployment (no certificates required).

### HTTPS profile

Files used:

- `compose.pi-https.yaml`
- `Infra/Caddyfile.pi`
- `UI/Dockerfile`
- `UI/Caddyfile`

Requirements:

- Place certificate files at:
  - `Infra/certs/cert.pem`
  - `Infra/certs/key.pem`
- Ensure your Pi deploy step writes runtime env vars to `.docker.env`.
- Include your HTTPS origin in `CORS_ORIGINS` (for example `https://mealprep.local`).

Run:

```bash
docker compose -f compose.pi-https.yaml up -d --build
```

### HTTP profile

Files used:

- `compose.pi-http.yaml`
- `Infra/Caddyfile.pi-http`
- `UI/Dockerfile`
- `UI/Caddyfile`

Notes:

- This profile sets `ASPNETCORE_ENVIRONMENT=Development` for API cookie compatibility over plain HTTP.
- Include your HTTP origin in `CORS_ORIGINS` (for example `http://192.168.1.98` or `http://mealprep.local`).

Run:

```bash
docker compose -f compose.pi-http.yaml up -d --build
```

### Verify deployment

HTTPS:

```bash
curl -kI https://mealprep.local
curl -kI https://mealprep.local/api/v1/me
```

HTTP:

```bash
curl -I http://192.168.1.98
curl -I http://192.168.1.98/api/v1/me
```
