# E2E Browser Tests (Playwright)

This directory contains a standalone Node.js Playwright app for browser-based end-to-end tests.

In a fork of the base repo, replace any placeholder deployment URLs before running these tests against your own app. In particular:

- `myapp.com`: replace with your real deployed application domain or localhost
- `myapp-ui.pages.dev`: replace with your preview/staging frontend hostname if you use Pages-style deployments

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
PLAYWRIGHT_BASE_URL="https://myapp.com" pnpm test
```

For a fork, point `PLAYWRIGHT_BASE_URL` at your own environment, for example `https://app.example.com`.

## Configuration

- `PLAYWRIGHT_BASE_URL` (required): target URL under test
- `E2E_MIN_SIMILARITY` (optional): default screenshot similarity threshold (`0.985`)

## Notes on visual checks

The suite uses pixel-based similarity (`pixelmatch`) and records first/second/diff screenshots as test attachments.
This gives visual stability coverage without requiring committed baseline image files.
