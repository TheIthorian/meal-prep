# Infra

This directory contains Railway provisioning helpers for the monorepo.

## What Is Here

- `railway.resources.yml`: declarative config for the Railway app service, Postgres, Redis, and S3-compatible bucket
- `environments/`: environment variable files that can be pushed to Railway
- `scripts/railway`: scripts to manage railway provisioning

The root [`railway.toml`](../railway.toml) stays at the repository root because Railway expects deployment config there.

## Scripts

| Script                   | Purpose                                                                                         |
| ------------------------ | ----------------------------------------------------------------------------------------------- |
| `pnpm railway:login`     | Starts Railway CLI authentication using the globally installed `railway` CLI.                   |
| `pnpm railway:link`      | Links the current repo to an existing Railway project.                                          |
| `pnpm railway:status`    | Shows the currently linked Railway project and environment status.                              |
| `pnpm railway:provision` | Creates the services defined in `railway.resources.yml`.                                        |
| `pnpm railway:patch`     | Applies the app service environment variables that reference the provisioned Railway resources. |
| `pnpm railway:push-env`  | Loads variables from a file in `environments/` and pushes them to the Railway app service.      |

## Prerequisites

- `pnpm` available locally
- Railway CLI installed globally and available as `railway`

## Install

From this directory:

```bash
pnpm install
```

Check your Railway auth and project link before running the scripts:

```bash
railway --version
pnpm railway:login
pnpm railway:status
```

If `railway status` says no linked project was found, run:

```bash
pnpm railway:link
```

## Config

Edit [`railway.resources.yml`](./railway.resources.yml) before provisioning.

Default values:

```yaml
project:
    environment: production

app:
    service: meal-prep
    repo: TheIthorian/meal-prep
    variables:
        ASPNETCORE_ENVIRONMENT: Production
        ASPNETCORE_HTTP_PORTS: '${{PORT}}'

resources:
    postgres:
        service: Postgres
    redis:
        service: Redis
    bucket:
        name: Bucket
        region: ams

patch:
    skipDeploys: false
```

Important notes:

Run commands without changing Railway state:

```bash
pnpm railway:provision -- --dry-run
pnpm railway:patch -- --dry-run
```

Use a different config file:

```bash
pnpm railway:provision -- --config ./my-config.yml
pnpm railway:patch -- --config ./my-config.yml
```

Push environment variables from a file:

```bash
pnpm railway:push-env -- --env-file ./environments/production.env
```

Dry-run an environment variable push:

```bash
pnpm railway:push-env -- --env-file ./environments/production.env --dry-run
```

## Environment Files

Store app-managed variables in `environments/*.env` files.

Suggested layout:

```text
environments/
  production.env
  staging.env
  local-preview.env
```

An example file is included at [`environments/production.env.example`](./environments/production.env.example).

The push script reads standard `.env` key/value pairs and sends them to Railway with `railway variable set`.

Keep these rules in mind:

- Do not commit secrets unless that is an intentional decision for your team.
- Prefer keeping Railway-managed infrastructure references in `pnpm railway:patch`.
- Use `pnpm railway:push-env` for app-level settings that are easier to maintain in files than in the Railway dashboard.
- These scripts shell out to the globally installed `railway` CLI, so make sure `railway` works in your shell before using them.
