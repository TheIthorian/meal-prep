# Codex Agent Instructions

## Global Rules

- Use `pnpm` for any Node.js/TypeScript/JavaScript dependency or script work (install, add, run, build).
- For backend queries that fetch workspace-owned resources, always include `.ForCurrentUser(...)` in the query chain.
- Always add docstrings for new (non-struct) classes. Exemptions: request/response objects, request/response validators, endpoint handler classes, and tests unless explicitly requested or truly necessary.
- Use extension blocks over static extension methods for new extension APIs.
- For serialized backend domain values, prefer string constants in a const/static class over enums.

## Backend (.NET / Api)

- Target .NET 10 and keep changes compatible with minimal APIs.
- Prefer `docker compose` (not `docker-compose`) for local service orchestration.
- When a service needs configuration, prefer a typed configuration/options object for that service over injecting `IConfiguration` directly into the service.
- For Hangfire/upload queue work, prefer one typed payload per job and one enqueue method per job type rather than a generic payload plus type switch. Payloads should inherit from the shared base that captures `AppExecutionContext` automatically.
- For backend logging, rely on the automatic trace/request/correlation context already added by the shared logging/telemetry helpers. Put stable, low-cardinality identifiers such as `workspaceId`, `uploadId`, `userId`, and counts into `BeginPropertyScope(...)` instead of repeating them in log message text.
  - Always try to leave a new line before and after logging (except before first or after the last line of a block)
- Tests require `.env.test` at the repo root; do not hardcode test secrets in code or workflow steps.
- When adding new endpoints, keep request/response DTOs in `Api/Endpoints/Responses` if they are shared.
  - Use extension methods when mapping from entities to responses
- Use Domain Exceptions (Api/Domain/DomainExceptions.cs) for any known errors. They will properly be handled by the Backend and UI.
- Models
  - To add a new data model, create a class in `Api/Models/` and then create a migration with `dotnet ef migrations add Add<model_name>`
  - To modify an existing model, update the model class and then create a migration with `dotnet ef migrations add Alter<model_name><very_brief_description>`
  - Never hand-edit EF migration or model snapshot files to compensate for missing `dotnet ef`. If `dotnet ef` is unavailable, stop and ask the user to run the migration command instead.

## Frontend (UI)

- Tech stack: Vite + React + TypeScript + Tailwind + shadcn-ui (see `UI/README.md`).
- Keep formatting consistent with existing lint/prettier config; run `pnpm` scripts instead of `npm` or `yarn`.
- Prefer `function` syntax for all non-anonymous functions, including curried or assigned functions. Use anonymous arrow functions only for inline callbacks where naming or hoisting is not needed.
- For currency or signed numeric values in the UI, explicitly prevent wrapping so negative signs do not break onto their own line; use `whitespace-nowrap` on the rendered value.
- UI analytics should track meaningful product behavior, not generic clicks already covered by autocapture.
- Prefer explicit PostHog events for key user flows and milestones: authentication, navigation between major contexts, successful create/update/delete actions, imports/uploads, onboarding steps, and other funnel transitions.
- Include stable, low-cardinality properties that help analyze flows, such as workspace ID, entity ID, counts, selected mode, or whether an optional step was used. Avoid high-cardinality or sensitive payloads such as free text, raw descriptions, emails, or unbounded user input.
- Track success states by default. Add failure or cancellation events only when they materially help explain drop-off, reliability, or recovery in a flow.
- Keep analytics calls centralized behind shared helpers when the same event schema or context is reused across multiple components.

## Tests

- Backend tests live in `Api.Tests`; prefer `dotnet test Api.Tests/Api.Tests.csproj`.
- If backend tests need to be executed, run them in the `myapp-dotnet` Docker container rather than on the host machine.
- If tests need infrastructure, rely on the docker compose services already defined in `compose.yaml`/`compose.test.yaml`.

## Responding to tasks

- Always consider updating this file (`AGENTS.md`) when you receive an instruction to change the style of approach in how you did something
