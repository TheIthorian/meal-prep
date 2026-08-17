# Environment Variables Reference

This file explains the runtime environment variables used by the API and deployment profiles.

For local Docker setup, copy `.docker.env.example` to `.docker.env`.
For Railway-style production setup, start from `Infra/environments/production.env.example`.

## App-inferred defaults (when unset)

- `AuthStateStore__Provider`: defaults to `Postgres` from `appsettings.json`/`appsettings.Development.json` (fallback is `redis` if no config value exists at all).
- `CORS_ORIGINS`: if unset, API allows these defaults:
  - `http://localhost:8080`
  - `http://localhost:5000`
  - `http://127.0.0.1:5500`
- `OTEL_ENABLED`: effectively `false` unless explicitly set to `true`.
- `OTEL_ENABLE_CONSOLE_EXPORTER`: effectively `false` unless explicitly set to `true`.
- `OTEL_EXPORTER_LOGS_ENDPOINT`: if unset, logs export endpoint falls back to `OTEL_EXPORTER_ENDPOINT`.
- `ConnectionStrings__DefaultConnection`: if unset, API tries fallback `POSTGRES_CONNECTIONSTRING`.
- `ConnectionStrings__Redis`: if unset, API tries fallback `REDIS_CONNECTIONSTRING`.
- `AppRoles`: required at startup. Fallback key name `APP_ROLES` is supported.

## Core host/runtime

- `HOST`: Bind address for the API process.
- `PORT`: App port value used in some deployment environments.
- `ASPNETCORE_ENVIRONMENT`: ASP.NET Core environment (`Development`, `Production`, etc.).
- `ASPNETCORE_HTTP_PORTS`: HTTP port(s) Kestrel should listen on.
- `ASPNETCORE_HTTPS_PORTS`: HTTPS port(s) Kestrel should listen on (optional if TLS is terminated by a proxy).
- `Logging__LogLevel__Default`: Default API log level override (for example `Warning`).
- `Logging__LogLevel__Microsoft__AspNetCore`: ASP.NET Core framework log level override.

## App roles

- `AppRoles`: Comma-separated app role list. Required.
  - Currently supported roles:
    - `worker:categorisation`
    - `worker:image-derivatives`: runs the background generation of resized recipe images.
  - `*` enables every role, which is what local development uses.
  - Example: `AppRoles=worker:categorisation,worker:image-derivatives`

## Database and cache

- `ConnectionStrings__DefaultConnection`: Primary Postgres connection string for EF Core and app data.
- `ConnectionStrings__Redis`: Redis connection string used for caching/auth state when Redis provider is selected.

## Auth state storage

- `AuthStateStore__Provider`: Where auth/data-protection keys are stored. Supported: `Redis`, `Postgres`.

## JWT


## S3 storage

- `S3__ServiceUrl`: S3-compatible endpoint (for example MinIO or AWS S3 endpoint URL).
- `S3__AccessKey`: S3 access key ID.
- `S3__SecretKey`: S3 secret access key.
- `S3__BucketName`: Bucket name for uploaded/managed objects.
- `S3__Region`: S3 region identifier.

## OpenAI-compatible API

- `OpenAI__ApiKey`: API key used for LLM features. Required for AI-powered features, but not required for the app to boot.
- `OpenAI__BaseUrl`: OpenAI-compatible base URL. The example files default to OpenRouter (`https://openrouter.ai/api/v1`).
- `OpenAI__Model`: Model ID used by LLM features. The example files default to `openai/gpt-4o-mini` for OpenRouter compatibility.

## CORS

- `CORS_ORIGINS`: Comma-separated allowed browser origins. Must exactly match your frontend origin(s), including scheme and port.

## Background processing

- `UPLOAD_CATEGORIZATION_BATCH_SIZE`: Batch size for upload categorization jobs.
- `RetentionCleanup__RetentionDays`: Retention window in days for cleanup jobs.
- `RecipeImageDerivatives__WorkerCount`: How many recipe image resizes may run at once (default `2`). Kept small on purpose: resizing is CPU bound, so this is the cap that stops a large import starving request handling.
- `RecipeImageDerivatives__PollIntervalSeconds`: How long a worker waits before checking the queue again when it is empty (default `5`).
- `RecipeImageDerivatives__MaxAttempts`: Attempts a single resize gets before the job is left failed and logged at error (default `5`).
- `RecipeImageDerivatives__RetryBackoffSeconds`: Base delay before a failed resize is retried, doubled per attempt (default `30`).
- `RecipeImageDerivatives__StaleClaimTimeoutMinutes`: How long a claimed job may sit before another worker may take it, which is how work interrupted by a restart is picked up again (default `10`).

## OpenTelemetry

- `OTEL_ENABLED`: Enables OpenTelemetry wiring when `true`.
- `OTEL_ENABLE_CONSOLE_EXPORTER`: Enables console exporter output for local/debug scenarios.
- `OTEL_EXPORTER_ENDPOINT`: OTLP traces endpoint.
- `OTEL_EXPORTER_LOGS_ENDPOINT`: OTLP logs endpoint (optional; falls back to trace endpoint if omitted in some setups).
- `OTEL_EXPORTER_API_KEY`: API key/header credential for telemetry backend.
- `OTEL_AXIOM_DATASET`: Axiom dataset name used in OTLP headers.

## Legacy/fallback variable names

These are supported by parts of the codebase for compatibility, but prefer the canonical names above:

- `APP_ROLES` (fallback for `AppRoles`)
- `POSTGRES_CONNECTIONSTRING` (fallback for `ConnectionStrings__DefaultConnection`)
- `REDIS_CONNECTIONSTRING` (fallback for `ConnectionStrings__Redis`)
