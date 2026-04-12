# API

This backend is the base ASP.NET Core 10 minimal API for the starter repo. Treat product-specific names in the codebase as placeholders until you replace them in your fork.

## Features

- User authentication with ASP.NET Core Identity (Bearer tokens and cookies)
- Multi-workspace support
- Account and transaction management
- Category hierarchy
- Currency support
- Redis caching
- Swagger API documentation

## Prerequisites

- .NET 10.0 SDK
- PostgreSQL database
- Redis server
- S3-compatible storage (MinIO in local/dev)

## Setup

For local development from the monorepo root, `docker compose up -d db redis minio minio-init` starts Postgres, Redis, and MinIO. On startup, the API creates the configured PostgreSQL database if it does not already exist and then applies EF Core migrations.

Before exposing this backend as your own service, update the placeholder identifiers called out in the root [README](../README.md#forking-checklist). The most important backend-specific ones are:

- `myapp_dev`: default development database name
- `myapp-dev`: default dev object-storage bucket name
- `myapp-autotest`: test/automation object-storage bucket name
- `myapp-net`: Docker network name used by local services
- `MyApp`: API/OpenAPI display name used in generated docs and UI text

1. **Configure the database connection string** in `appsettings.json` or `appsettings.Development.json`:

   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Host=localhost;Database=myapp_dev;Username=root;Password=password"
   }
   ```

2. **Choose auth state/key storage provider** (prevents logout on server restart):

   ```json
   "AuthStateStore": {
     "Provider": "Postgres"
   }
   ```

   Supported values:
   - `Postgres`: stores Data Protection keys in PostgreSQL
   - `Redis`: stores Data Protection keys in Redis (requires `ConnectionStrings:Redis`)

3. **Run database migrations**:

   ```bash
   dotnet tool restore
   dotnet ef database update
   ```

   First-time setup for local tools:

   ```bash
   dotnet new tool-manifest
   dotnet tool install dotnet-ef
   ```

   If you are running from the repo root instead of `Api/`:

   ```bash
   dotnet tool restore
   dotnet ef database update --project Api --startup-project Api
   ```

   If `dotnet ef` reports missing packages after a restore from a different environment, delete
   `Api/obj` and `Api.Tests/obj`, run `dotnet restore myapp-dotnet.sln` from the repo root,
   and then retry the migration command.

4. **Run the application**:

   ```bash
   dotnet run
   ```

5. **Access Swagger UI** (in development mode):
   Navigate to `https://localhost:5001/swagger` or `http://localhost:5000/swagger`

## Typical Base Repo Workflow

From the repo root:

```bash
docker compose up -d
dotnet run --project Api
```

That is the standard way to use the API when this repo is acting as a base template.

## Docker

Build and run with Docker:

```bash
docker build -t myapp-api .
docker run -p 5000:5000 -p 5001:5001 myapp-api
```

Ensure PostgreSQL is accessible from the container. Redis is only required when `AuthStateStore.Provider=Redis`.

For local backend development/tests in this monorepo, start shared dependencies from the repo root:

```bash
docker compose up -d db redis minio minio-init
```
