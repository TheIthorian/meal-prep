# Meal Prep

This frontend is the Meal Prep web app. It provides the user-facing experience for authentication, workspace navigation, and the recipe-to-shopping-list workflow.

## Base Repo Usage

For normal local development from the monorepo:

1. Start backend dependencies from the repo root with `docker compose up -d`.
2. Run the API with `dotnet run --project Api`.
3. Install workspace dependencies once from the repo root with `pnpm install`.
4. Run the UI from the repo root with `pnpm dev` or from this package with `pnpm dev`.

## What technologies are used for this project?

This project is built with:

- Vite
- TypeScript
- React
- shadcn-ui
- Tailwind CSS
