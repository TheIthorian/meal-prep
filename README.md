# Meal Prep

Meal Prep is a self-hostable recipe and shopping workflow app.
It lets you save recipes, import recipes from URLs, organize ingredients, generate shopping lists, and connect MCP clients to a workspace.

## Quickstart (Docker Compose)

### Prerequisites

- Docker with Compose plugin (`docker compose`)

### 1) Create `.docker.env` from the template

From repo root:

```bash
cp .docker.env.example .docker.env
```

Then update at least these keys in `.docker.env`:

```env
Jwt__Issuer=meal-prep.local
Jwt__Audience=meal-prep.local
Jwt__Key=replace-with-a-long-random-secret
OpenAI__BaseUrl=https://api.openai.com/v1
OpenAI__ApiKey=replace-with-your-api-key
CORS_ORIGINS=http://localhost,http://localhost:80,http://localhost:5001
```

Notes:

- `OpenAI__ApiKey` is required for AI-powered features such as AI recipe import/OCR, ingredient categorization, and AI tag suggestions.
- The app can still start and non-AI features can still work without an OpenAI key.
- `OpenAI__BaseUrl` lets you switch to an OpenAI-compatible provider such as OpenRouter.
- [OpenRouter](https://openrouter.ai/) is encouraged for better observability and uptime across AI requests.
- Get an OpenRouter API key from [OpenRouter Keys](https://openrouter.ai/keys).

### 2) Build and run

From repo root:

```bash
docker compose -f compose.http.yaml up -d --build
```

This starts API + PostgreSQL + Redis + MinIO.

### 3) Verify

```bash
docker compose ps
curl -I http://localhost:5001/api/health
```

## What To Read Next

- Deployment options: [`Docs/deploy.md`](./Docs/deploy.md)
- Environment variables reference: [`Docs/env-vars.md`](./Docs/env-vars.md)
- HTTPS setup notes: [`Docs/https.md`](./Docs/https.md)
- MCP server usage and deployment caveats: [`Docs/mcp.md`](./Docs/mcp.md)
- Turborepo workflow: [`Docs/turborepo.md`](./Docs/turborepo.md)
- Feature guide: [`Docs/features.md`](./Docs/features.md)

## Project READMEs

- API: [`Api/README.md`](./Api/README.md)
- UI: [`UI/README.md`](./UI/README.md)
- End-to-end tests: [`E2eTests/README.md`](./E2eTests/README.md)
- Infrastructure tooling: [`Infra/README.md`](./Infra/README.md)
