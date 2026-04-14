# Turborepo Workflow

This repository is a Turborepo monorepo managed with `pnpm`.

## Common Root Commands

From the repository root:

```bash
pnpm build
pnpm lint
pnpm typecheck
pnpm dev
pnpm test
```

## Meal Prep Specific Scripts

```bash
pnpm api:build
pnpm api:dev
pnpm api:run
pnpm api:test
pnpm test:e2e
pnpm install:browsers
```

## Target A Single Workspace

```bash
pnpm --filter meal-prep-api build
pnpm --filter meal-prep-api run
pnpm --filter meal-prep-api-tests test
pnpm --filter meal-prep-ui build
pnpm --filter meal-prep-infra railway:status
pnpm --filter meal-prep-e2e-tests test
```

## Package Names

- `meal-prep-api`
- `meal-prep-api-tests`
- `meal-prep-ui`
- `meal-prep-infra`
- `meal-prep-e2e-tests`

## Notes

- Always run scripts with `pnpm` in this repo.
- Use root scripts for day-to-day workflows and `--filter` for focused tasks.
