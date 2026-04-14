# E2E Browser Tests (Playwright)

This directory contains the Playwright workspace in the Turborepo monorepo.

Set `PLAYWRIGHT_BASE_URL` to the deployed UI you want to test, such as local, staging, or production.

## What is covered

- Protected-route redirect to `/login`
- Login -> Register navigation
- Client-side registration validation
- Legal pages (`/terms`, `/data-retention`)
- Disposable-user registration and login flow
- Screenshot similarity checks for key views

## Prerequisites

- Node.js 20+
- A reachable deployed UI URL to run tests against

## Run locally

```bash
pnpm install
pnpm install:browsers
PLAYWRIGHT_BASE_URL="https://meal-prep.example.com" pnpm test:e2e
```

Point `PLAYWRIGHT_BASE_URL` at the environment you want to validate, for example `http://localhost` for local Docker deployment or a staging/prod URL.

Run the package directly if you only want this workspace:

```bash
pnpm --filter meal-prep-e2e-tests test
```

## Configuration

- `PLAYWRIGHT_BASE_URL` (required): target URL under test
- `E2E_MIN_SIMILARITY` (optional): default screenshot similarity threshold (`0.985`)

## Typical monorepo flow

From repo root:

1. Start app stack with `docker compose up -d --build`
2. Set `PLAYWRIGHT_BASE_URL` to the UI origin
3. Run `pnpm test:e2e`

## Notes on visual checks

The suite uses pixel-based similarity (`pixelmatch`) and records first/second/diff screenshots as test attachments.
This gives visual stability coverage without requiring committed baseline image files.
