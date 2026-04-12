# MY-APP

This frontend is the base Vite + React UI for the starter repo. When you fork the template, replace the placeholder product strings from the root [README](../README.md), especially:

- `MY-APP`: uppercase project label used in headings
- `myapp-ui`: frontend service or deployment name
- `myapp-ui.pages.dev`: preview/static-hosting hostname
- `myapp.com`: production app hostname
- `MyApp`: display name shown in the UI
- `MyAppDescription`: short marketing/product tagline
- `MyAppLongDescription`: longer metadata/SEO description

## Base Repo Usage

For normal local development from the monorepo:

1. Start backend dependencies from the repo root with `docker compose up -d`.
2. Run the API with `dotnet run --project Api`.
3. Run the UI from `UI/` with `pnpm install` and `pnpm run dev`.

Update the placeholder names before publishing, deploying, or sharing the fork as your own product.

## What technologies are used for this project?

This project is built with:

- Vite
- TypeScript
- React
- shadcn-ui
- Tailwind CSS
