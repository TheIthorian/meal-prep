# API

This backend powers Meal Prep, a web app for collecting recipes, importing recipes from the web, and turning recipe
ingredients into shopping lists.

## Features

- User authentication with ASP.NET Core Identity (Bearer tokens and cookies)
- Multi-workspace support
- Recipe and shopping-list application foundations
- Swagger API documentation

## Prerequisites

(only when running API outside Docker Compose)

- .NET 10.0 SDK
- PostgreSQL database
- S3-compatible storage, such as MinIO
- Redis

## Setup

For local development, start dependencies from the monorepo root:

```bash
docker compose up -d db redis minio minio-init
```

On startup, the API creates the configured PostgreSQL database if it does not already exist and applies EF Core
migrations.

1. **Configure environment variables** (recommended).

   Copy `Api/.env.example` to your local `.env` file and set values for your environment.

   Common keys:
   - `ConnectionStrings__DefaultConnection`
   - `ConnectionStrings__Redis`
   - `AuthStateStore__Provider`
   - `Jwt__Issuer`, `Jwt__Audience`, `Jwt__Key`
   - `S3__ServiceUrl`, `S3__AccessKey`, `S3__SecretKey`, `S3__BucketName`, `S3__Region`

   `appsettings.json` and `appsettings.Development.json` can still be used for defaults, but environment variables are the primary approach.

2. **Choose auth state/key storage provider** (prevents logout on server restart):

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
   `Api/obj` and `Api.Tests/obj`, run `dotnet restore meal-prep.sln` from the repo root,
   and then retry the migration command.

4. **Run the application**:

   ```bash
   dotnet run
   ```

5. **Access Swagger UI** (in development mode):
   Navigate to `http://localhost:5001/swagger`

## Typical Base Repo Workflow

From the repo root:

```bash
docker compose up -d
dotnet run --project Api
```

That is the standard local workflow for backend development.

You can also use root `pnpm` scripts (for example `pnpm api:dev`, `pnpm api:build`, `pnpm api:test`) because this repository uses Turborepo to orchestrate workspace commands.

For full Dockerized app startup and deployment notes, see the repo root [`README.md`](../README.md) and [`Docs/deploy.md`](../Docs/deploy.md).
