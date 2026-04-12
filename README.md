# Dotnet Base Monorepo

A starter monorepo for building an ASP.NET Core application with a React frontend, Playwright end-to-end tests, and local development infrastructure.

Use this repository as a base template. After forking, replace the placeholder app names, hostnames, container names, and storage/database identifiers listed in [Forking Checklist](#forking-checklist) before treating the fork as your product repo.

## Projects

| Project  | Description                              | Link                                       |
| -------- | ---------------------------------------- | ------------------------------------------ |
| Api      | ASP.NET Core minimal API backend         | [Api/README.md](./Api/README.md)           |
| UI       | Vite + React + TypeScript frontend       | [UI/README.md](./UI/README.md)             |
| E2eTests | Playwright browser end-to-end test suite | [E2eTests/README.md](./E2eTests/README.md) |

## Quick Start

Use these steps when starting from the base repo:

```bash
# Start local infrastructure
docker compose up -d

# Build the solution
dotnet build

# Run the backend
dotnet run --project Api

# Run the frontend in another shell
cd ../UI
pnpm install
pnpm run dev
```

This brings up:

- PostgreSQL for application data
- Redis for caching and auth-state storage
- MinIO for S3-compatible object storage
- OpenTelemetry Collector for local observability plumbing

## Forking Checklist

After you fork this repository, replace every placeholder string below with values for your app. A quick way to audit remaining placeholders is:

```bash
rg -n "myapp-dev|myapp-autotest|myapp_dev|myapp-net|myapp\\.com|myapp-ui|myapp-redis|myapp-postgres|myapp-ui\\.pages\\.dev|MY-APP|myapp|MyApp|MyAppDescription|MyAppLongDescription"
```

| Placeholder            | Replace with                                             | Description                                                                      |
| ---------------------- | -------------------------------------------------------- | -------------------------------------------------------------------------------- |
| `myapp-net`            | Your Docker network name                                 | Shared Docker Compose network for app containers.                                |
| `myapp-dev`            | Your primary dev object-storage bucket/container name    | Used for local/dev S3-compatible storage such as MinIO buckets.                  |
| `myapp-autotest`       | Your automated test object-storage bucket/container name | Used by tests or isolated automation flows that should not share the dev bucket. |
| `myapp_dev`            | Your development database name                           | Used in local connection strings and database bootstrap configuration.           |
| `myapp.com`            | Your production marketing or app domain                  | Used anywhere docs or tests need the real deployed hostname.                     |
| `myapp-ui`             | Your frontend service/app name                           | Used for frontend-specific service naming and deployment identifiers.            |
| `myapp-redis`          | Your Redis container/service name                        | Used in Docker and local infrastructure naming.                                  |
| `myapp-postgres`       | Your PostgreSQL container/service name                   | Used in Docker and local infrastructure naming.                                  |
| `myapp-ui.pages.dev`   | Your preview deployment hostname                         | Used for Cloudflare Pages or equivalent preview/static-hosting domains.          |
| `MY-APP`               | Your uppercase product slug                              | Used where docs or config expect a loud, title-banner style identifier.          |
| `myapp`                | Your lowercase canonical slug                            | Used for package names, short identifiers, and machine-readable values.          |
| `MyApp`                | Your product display name                                | Used in UI text, page titles, OpenAPI metadata, and human-readable labels.       |
| `MyAppDescription`     | Your short one-line product description                  | Used in login pages, titles, and short marketing copy.                           |
| `MyAppLongDescription` | Your longer SEO/product description                      | Used in metadata and other longer-form descriptive copy.                         |

## Backend Test Dependencies

Endpoint/integration tests in `Api.Tests` expect backend dependencies to be available:

- PostgreSQL on `127.0.0.1:5432`
- Redis on `127.0.0.1:6379`
- MinIO on `127.0.0.1:9000`

Start them with:

```bash
docker compose up -d
```

The test suite reads environment variables from `.env.test` at repo root. No other env/config sources are used for tests.

## Test Container

Run a dedicated test container (keeps a shell open for `dotnet test` and uses `.env.test`):

```bash
docker compose -f compose.yaml -f compose.test.yaml up -d
docker compose -f compose.yaml -f compose.test.yaml exec tests bash
```

Then inside the container:

```bash
dotnet test Api.Tests/Api.Tests.csproj
```

## Infrastructure

- **Database**: PostgreSQL
- **Cache**: Redis
- **Storage**: MinIO
- **Observability**: OpenTelemetry
- **CI/CD**: GitHub Actions
- **Container Registry**: replace the sample registry target with your own registry/repository name in your fork
