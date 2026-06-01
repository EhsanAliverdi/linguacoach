# LinguaCoach

AI-powered language learning SaaS — role-specific workplace English for immigrant professionals.

**Current status:** Backend API complete (T1–T4). Angular frontend coming in T6.

---

## Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 10.0+ |
| Docker Desktop | 4.x+ |
| PostgreSQL (optional) | 17 (provided by Docker Compose) |

---

## Running with Docker Compose (recommended)

This starts the API and a PostgreSQL database together. EF Core migrations run automatically on API startup.

```bash
# Optional: set a real JWT key (required outside Development)
export JWT_KEY="your-secret-key-at-least-32-chars"

docker compose up --build
```

| Service | URL |
|---------|-----|
| API | http://localhost:8080 |
| Health check | http://localhost:8080/health |
| OpenAPI (Development only) | http://localhost:8080/openapi/v1.json |
| PostgreSQL | localhost:5433 (host port; 5432 inside Docker network) |

To stop and remove volumes:

```bash
docker compose down -v
```

---

## Running locally without Docker

### 1. Start PostgreSQL

You need a running PostgreSQL instance. The default connection string expects:

```
Host=localhost;Database=linguacoach_dev;Username=postgres;Password=postgres
```

Override via environment variable or `appsettings.Development.json`:

```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Database=linguacoach_dev;Username=postgres;Password=postgres"
```

### 2. Run the API

```bash
cd src/LinguaCoach.Api
dotnet run
```

EF Core migrations run automatically on startup. The API listens on `https://localhost:7xxx` and `http://localhost:5xxx` (ports shown in terminal output).

---

## Running tests

Tests use an in-memory SQLite database — no PostgreSQL required.

```bash
dotnet test
```

Expected output: **158 tests, 0 failures** (113 unit + 45 integration).

---

## Environment variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `ConnectionStrings__DefaultConnection` | Yes (non-Docker) | localhost dev string in `appsettings.json` | PostgreSQL connection string |
| `JWT_KEY` | Yes (non-Development) | Placeholder in `appsettings.json` | JWT signing secret — must be ≥ 32 chars and not the placeholder outside Development |
| `Jwt__Issuer` | No | `linguacoach` | JWT issuer |
| `Jwt__Audience` | No | `linguacoach` | JWT audience |
| `Jwt__ExpiryHours` | No | `24` | JWT access token lifetime in hours |
| `ASPNETCORE_ENVIRONMENT` | No | `Production` in Docker, `Development` locally | Controls OpenAPI exposure and JWT placeholder guard |

> **Security:** The `appsettings.json` JWT key (`CHANGE_ME_IN_PRODUCTION_USE_A_SECRET_AT_LEAST_32_CHARS`) is valid only in `Development`. The API refuses to start with that placeholder in any other environment.

---

## Project structure

```
src/
  LinguaCoach.Domain/          Domain entities, enums, exceptions — no EF/Identity dependency
  LinguaCoach.Application/     Interfaces and use-case contracts
  LinguaCoach.Persistence/     EF Core, migrations, Identity, PostgreSQL
  LinguaCoach.Infrastructure/  JWT, handlers, no-op STT/TTS stubs
  LinguaCoach.Api/             Controllers, Program.cs
tests/
  LinguaCoach.UnitTests/       Domain logic tests (FluentAssertions + xUnit)
  LinguaCoach.IntegrationTests/ API endpoint tests (WebApplicationFactory + SQLite)
docs/
  implementation-roadmap.md   T1–T12 task plan
```

---

## API endpoints

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/auth/login` | None | Login → JWT token |
| POST | `/api/auth/change-password` | JWT | Change password (clears MustChangePassword) |
| POST | `/api/admin/students` | Admin JWT | Create student account |
| PATCH | `/api/onboarding` | JWT | Submit one onboarding step |
| GET | `/api/onboarding/status` | JWT | Current onboarding state |
| GET | `/api/dashboard` | JWT | Dashboard (requires completed onboarding) |
| GET | `/api/reference/language-pairs` | JWT | Active language pairs |
| GET | `/api/reference/tracks?languagePairId=` | JWT | Learning tracks for a language pair |
| GET | `/api/reference/career-profiles?languagePairId=` | JWT | Career profiles for a language pair |
| GET | `/health` | None | Health check |

---

## Frontend

The Angular frontend is coming in **T6**. Until then, the API can be exercised directly via curl, Postman, or the OpenAPI endpoint (in Development).

---

## CI/CD

GitHub Actions runs on every push to `main` and on pull requests:
- `dotnet restore`
- `dotnet build`
- `dotnet test`

Angular build step and deployment are placeholders until T6 and the Fly.io target are confirmed. See `.github/workflows/ci.yml`.
