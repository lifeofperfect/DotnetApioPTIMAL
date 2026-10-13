# OptimalApi

ASP.NET Core (.NET 10) CRUD API on PostgreSQL, run locally with Docker.

## Prerequisites

- .NET SDK 10
- Docker Desktop

## First-time setup

```bash
cp .env.example .env              # then set POSTGRES_PASSWORD (skip if .env already exists)
dotnet tool restore               # installs dotnet-ef pinned in .config/dotnet-tools.json

# Store the dev connection string outside the repo (reads the password from .env)
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Port=5432;Database=optimalapi;Username=app;Password=$(grep POSTGRES_PASSWORD .env | cut -d= -f2)" \
  --project src/OptimalApi.Api
```

## Run

```bash
docker compose up -d db                                  # start Postgres
dotnet ef database update --project src/OptimalApi.Api   # apply migrations
dotnet run --project src/OptimalApi.Api                  # http://localhost:5080
```

| Endpoint | Purpose |
| --- | --- |
| `GET /health/live` | Process is up (never touches the database) |
| `GET /health/ready` | Postgres is reachable (503 within 5s if not) |
| `GET /openapi/v1.json` | OpenAPI document (Development only) |

## Migrations

```bash
dotnet ef migrations add <Name> --project src/OptimalApi.Api --output-dir Infrastructure/Migrations
dotnet ef database update --project src/OptimalApi.Api
```

## Test

```bash
dotnet test
```
