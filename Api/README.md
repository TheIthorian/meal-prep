# API

This backend powers Meal Prep, a web app for collecting recipes, importing recipes from the web, and turning recipe ingredients into shopping lists.

## Features

- User authentication with ASP.NET Core Identity (Bearer tokens and cookies)
- Multi-workspace support
- Recipe and shopping-list application foundations
- Swagger API documentation

## Prerequisites

- .NET 10.0 SDK
- PostgreSQL database
- S3-compatible storage (MinIO in local/dev)

## Setup

For local development from the monorepo root, `docker compose up -d db minio minio-init` starts Postgres and MinIO. On startup, the API creates the configured PostgreSQL database if it does not already exist and then applies EF Core migrations.

1. **Configure the database connection string** in `appsettings.json` or `appsettings.Development.json`:

   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Host=localhost;Database=meal_prep_dev;Username=root;Password=password"
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
   `Api/obj` and `Api.Tests/obj`, run `dotnet restore meal-prep.sln` from the repo root,
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

That is the standard local workflow for backend development.

## Docker

Build and run with Docker:

```bash
docker build -t meal-prep-api .
docker run -p 5000:5000 -p 5001:5001 meal-prep-api
```

Ensure PostgreSQL is accessible from the container. Redis is only required when `AuthStateStore.Provider=Redis`.

For local backend development/tests in this monorepo, start shared dependencies from the repo root:

```bash
docker compose up -d db minio minio-init
```
