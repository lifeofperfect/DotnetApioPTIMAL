# OptimalApi

ASP.NET Core (.NET 10) CRUD API on PostgreSQL, run locally with Docker.

## Prerequisites

- .NET SDK 10
- Docker Desktop

## Option A: run everything in Docker (no .NET SDK needed)

```bash
cp .env.example .env            # then set POSTGRES_PASSWORD (skip if .env already exists)
docker compose up --build -d    # db -> migrate (applies migrations, exits) -> api
```

The API is at http://localhost:8080 (change `API_PORT` in `.env` if 8080 is taken).
Stop with `docker compose down` (data is kept in the `pgdata` volume; add `-v` to wipe it).

## Option B: run the API locally (for development and debugging)

First time only:

```bash
cp .env.example .env              # then set POSTGRES_PASSWORD (skip if .env already exists)
dotnet tool restore               # installs dotnet-ef pinned in .config/dotnet-tools.json

# Store the dev connection string outside the repo (reads the password from .env)
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Port=5432;Database=optimalapi;Username=app;Password=$(grep POSTGRES_PASSWORD .env | cut -d= -f2)" \
  --project src/OptimalApi.Api
```

Every time:

```bash
docker compose up -d db                                  # start only Postgres
dotnet ef database update --project src/OptimalApi.Api   # apply migrations
dotnet run --project src/OptimalApi.Api                  # http://localhost:5080
```

## API

Base URL: http://localhost:8080 (Docker) or http://localhost:5080 (local `dotnet run`).

| Endpoint | Purpose |
| --- | --- |
| `GET /api/products?page=1&pageSize=20` | List products (pageSize 1–100) |
| `GET /api/products/{id}` | Get one product, including its `version` |
| `POST /api/products` | Create: `{ "name": "...", "price": 9.99 }` → 201 |
| `PUT /api/products/{id}` | Update: `{ "name", "price", "version" }` → 200, or 409 if `version` is stale |
| `DELETE /api/products/{id}` | Delete → 200 |
| `GET /health/live` | Process is up (never touches the database) |
| `GET /health/ready` | Postgres is usable (503 within 5s if not) |
| `GET /health` | Every check with details as JSON (Development only) |
| `GET /health-ui` | Health dashboard with history (Development only) |
| `GET /openapi/v1.json` | OpenAPI document (Development only) |

Every response, success or error, uses the same envelope:

```json
{ "success": true,  "message": "Product retrieved.", "data": { ... }, "errors": null }
{ "success": false, "message": "Validation failed.", "data": null, "errors": { "name": ["Name is required."] }, "traceId": "00-..." }
```

Errors: 400 validation (per-field `errors`), 404, 405, 409 concurrent edit, 415 wrong content type,
500 unexpected (details are logged under the same `traceId`, never sent to the client).

To try every endpoint, open `src/OptimalApi.Api/OptimalApi.Api.http` in Rider and run the requests top to bottom
(it targets the local `dotnet run` port 5080; change `@baseUrl` to 8080 when using Docker).

## Health checks

| Check | What it watches | Degraded | Unhealthy |
| --- | --- | --- | --- |
| `database` | Postgres answers, schema up to date | query over 500 ms, or pending migrations | can't reach Postgres |
| `database-connections` | share of Postgres's connection limit in use | over 80% | over 95% |
| `database-activity` | stuck queries and locks | a query running over 30 s, a session waiting on a lock, or a transaction left open | (never) |
| `database-size` | database size and product row estimate | over 10 GB | (never) |
| `thread-pool` | thread pool starvation (blocking calls) | over 50 work items queued | (never) |
| `garbage-collection` | time spent paused for GC | over 10% | (never) |
| `memory` | process memory | over 1 GB | (never) |
| `disk-space` | free disk where the app runs | under 5 GB or 5% free | under 1 GB free |
| `app-info` | version, git commit, environment, uptime, runtime | (always Healthy: information only) | |

Only `database` affects `/health/ready`. The others are information for `/health` and the dashboard, so a warning
such as a slow GC or a long query never takes the app out of the load balancer.

Each check also reports its numbers (latency, connections, sizes, thread counts and so on) in `/health`.
`/health-ui` polls `/health` every 15 s and keeps a short history per check; open it at
http://localhost:5080/health-ui (`dotnet run`) or http://localhost:8080/health-ui (Docker).

`/health` and `/health-ui` reveal internals, so they are only enabled in Development. To use them in production,
put them behind authentication first.

## Migrations

```bash
dotnet ef migrations add <Name> --project src/OptimalApi.Api --output-dir Infrastructure/Migrations
dotnet ef database update --project src/OptimalApi.Api
```

## Test

```bash
dotnet test
```

- `tests/OptimalApi.UnitTests`: validation rules, no dependencies.
- `tests/OptimalApi.IntegrationTests`: the full API over HTTP against a real, throwaway PostgreSQL
  started by Testcontainers. **Docker must be running.** Nothing touches your dev database.
