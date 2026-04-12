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
docker compose up -d
dotnet build
dotnet run --project Api
```

In another shell:

```bash
cd UI
pnpm install
pnpm run dev
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

## Infrastructure

- Database: PostgreSQL
- Cache: Redis
- Storage: MinIO
- Observability: OpenTelemetry
- CI/CD: GitHub Actions
