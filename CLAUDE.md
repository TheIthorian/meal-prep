# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Meal Prep API: An ASP.NET Core application for recipe import and shopping-list workflows with supporting infrastructure and authentication.

## Development Commands

### Build

```bash
# Build the application
dotnet build

# Build Docker containers
docker compose build

# Start development environment
docker compose up -d
```

### Running Tests

```bash
# Run unit tests
dotnet test

# Run specific test
dotnet test --filter "TestCategory=YourCategory"
```

### API Development

```bash
# Launch development server
dotnet run

# Generate REST client requests
# Use meal-prep-api.http in Visual Studio Code or JetBrains Rider
```

## Key Architecture Components

### Infrastructure

- ASP.NET Core 10.0 (RC)
- Docker containerization
- PostgreSQL database
- Redis cache
- MinIO object storage
- OpenTelemetry observability

### Authentication

- Azure AD B2C
- JWT Bearer tokens
- Scope-based authorization
- OpenID Connect flow

### Development Principles

- Minimal API design
- Environment-based configuration
- Containerized multi-service architecture
- Explicit dependency injection

### Dependencies

Pin every dependency to an exact version — no `^`, `~`, or floating ranges in any
`package.json`, and no wildcard `Version=` in `.csproj`. Builds must be reproducible from
the lockfile alone.

```bash
# Adding a package (pnpm workspaces)
pnpm add --save-exact <package>          # run inside the workspace that needs it, e.g. UI/
pnpm install --frozen-lockfile           # verify the lockfile still resolves
```

Commit the updated `pnpm-lock.yaml` with the `package.json` change. If a lockfile edit
produces churn unrelated to the package being added (peer-dependency rewrites from a
different pnpm version), revert it and re-apply only the intended lines.

## Configuration Management

### Environment Variables

Critical configuration is managed through:

- `appsettings.json`
- `appsettings.Development.json`
- `.docker.env`

Sensitive information like credentials should be managed through:

- Azure Key Vault (recommended)
- User Secrets for local development
- Environment-specific configuration

## Development Workflow Notes

1. Always use environment-specific configurations
2. Leverage Docker Compose for consistent local development
3. Utilize OpenTelemetry for distributed tracing and observability
4. Implement proper scope-based authorization for endpoints

## Recommended IDE

- Visual Studio 2022
- Visual Studio Code with C# extension
- JetBrains Rider

## Contribution Guidelines

- Follow existing code structure and naming conventions
- Implement comprehensive unit and integration tests
- Use dependency injection for loose coupling
- Maintain security by using Azure AD B2C scopes

## Performance Considerations

- Utilize Redis for distributed caching
- Implement efficient database queries
- Use minimal API endpoints for lightweight routing
