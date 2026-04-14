# Meal Prep

This frontend is the Meal Prep web app. It provides the user-facing experience for authentication, workspace navigation, and the recipe-to-shopping-list workflow.

## Prerequisites

- Node.js 20+
- `pnpm`
- Running backend stack (API + Postgres + Redis + MinIO)

## Base Repo Usage

For normal local development from the monorepo:

```bash
# start backend dependencies
docker compose up -d

# run the API
dotnet run --project Api

# install monorepo dependencies (first time or when lockfile changes)
pnpm install

# run the UI dev server
pnpm dev
```

The default UI dev URL is `http://localhost:8080`.

## Production-style UI check

If you want to test a production-style build locally:

```bash
pnpm --filter meal-prep-ui build
pnpm --filter meal-prep-ui preview
```

## Technology

This project is built with:

- Vite
- TypeScript
- React
- shadcn-ui
- Tailwind CSS

For deployment variants (Self hosted HTTP/HTTPS, Docker host), see [`README.md`](../README.md) and [`Docs/deploy.md`](../Docs/deploy.md) at the repo root.
