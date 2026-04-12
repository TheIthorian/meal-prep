# E2E Browser Tests (Playwright)

This directory contains a standalone Node.js Playwright app for browser-based end-to-end tests.

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
cd E2eTests
pnpm install
pnpm run install:browsers
PLAYWRIGHT_BASE_URL="https://meal-prep.example.com" pnpm test
```

Point `PLAYWRIGHT_BASE_URL` at the environment you want to validate, for example `https://app.example.com`.

## Configuration

- `PLAYWRIGHT_BASE_URL` (required): target URL under test
- `E2E_MIN_SIMILARITY` (optional): default screenshot similarity threshold (`0.985`)

## Notes on visual checks

The suite uses pixel-based similarity (`pixelmatch`) and records first/second/diff screenshots as test attachments.
This gives visual stability coverage without requiring committed baseline image files.
