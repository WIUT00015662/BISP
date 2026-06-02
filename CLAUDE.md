# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

BISP is a game price aggregator — a full-stack app that tracks game prices across Steam, GOG, and Epic Games, manages wishlists, and notifies subscribed users of discounts.

## Architecture

Two independently running processes:

- **Backend**: ASP.NET Core Web API (.NET 10) — `backend/`
- **Frontend**: React 19 SPA (Vite 8) — `frontend/`
- **Tests**: xUnit test project — `Bisp.Api.Tests/`

The frontend calls the backend directly (no proxy). CORS is configured to allow `http://localhost:5173`.

### Backend structure

- `Program.cs` — composition root; registers all services, middleware, seeding. Uses `AddIdentityCore` (not `AddIdentity`) so `SignInManager` and roles are added explicitly.
- `Controllers/` — `AuthController`, `GamesController`, `WishlistController`, `SubscriptionsController`, `AdminController`, `HealthController`
- `Data/AppDbContext.cs` — EF Core 10, extends `IdentityDbContext<ApplicationUser>`, SQL Server
- `Models/` — EF Core entities: `Game`, `Store`, `GameStorePrice`, `ExternalGameId`, `WishlistItem`, `UserSubscription`, `NotificationLog`
- `Options/` — strongly-typed config classes bound to `appsettings.json` sections via `IOptions<T>`
- `Services/AggregationWorker.cs` — `BackgroundService` using `PeriodicTimer`; fires `AggregationService.RunAsync()` every `Aggregation:IntervalHours` (default: 10)
- `Services/NotificationWorker.cs` — `BackgroundService`; fires `NotificationService.CheckWishlistAsync()` periodically
- `Services/AdminSeeder.cs` — seeds `Admin` role and user at startup

### Game selection pipeline

Games are selected by combining IGDB metadata with live store pricing:

1. **IGDB query** (`IgdbService.GetTopGamesWithStorePresenceAsync`) — fetches up to 100 candidate games from IGDB sorted by `total_rating_count desc` (most-reviewed games first). Filters to main games only (`category = 0`, `version_parent = null`) that have external IDs for at least one of Steam (category 1), GOG (category 5), or Epic (category 26). Fetches in batches of 50 (IGDB API max), up to offset 500.

2. **Cross-platform filter** (`IgdbService.ParseGame`) — candidate must have a Steam ID **and** at least one of GOG ID or Epic slug. Steam-only games are dropped.

3. **Live price verification** (`AggregationService.RunAsync`) — for each candidate, fetches current prices from each available store. Game is accepted only if it has live prices from **≥ 2 stores**.

4. **Target**: `TargetGameCount = 20` games per aggregation run. Stops early once 20 games are confirmed, otherwise exhausts the 100 IGDB candidates.

### Auth model

ASP.NET Core Identity + JWT Bearer. Tokens use issuer `Bisp.Api`, audience `Bisp.Spa`. Email confirmation is currently disabled (`RequireConfirmedEmail = false`). `[Authorize(Roles = "Admin")]` guards admin routes.

### API endpoints

| Controller | Route prefix | Auth |
|---|---|---|
| AuthController | `/api/auth` | Public |
| GamesController | `/api/games` | Public |
| WishlistController | `/api/wishlist` | `[Authorize]` |
| SubscriptionsController | `/api/subscriptions` | `[Authorize]` |
| AdminController | `/api/admin` | `[Authorize(Roles="Admin")]` |
| HealthController | `/health` | Public |

### Known stubs / incomplete

- Frontend `App.jsx` — still the Vite scaffold template
- `EmailSender` falls back to a no-op log warning when `Smtp:Host` is not configured (otherwise uses MailKit)

## Commands

### Backend

```bash
dotnet run --project backend/Bisp.Api.csproj       # run API (http://localhost:5258)
dotnet build backend/Bisp.Api.csproj               # build only

# EF Core migrations (run from repo root)
dotnet ef migrations add <Name> --project backend/Bisp.Api.csproj
dotnet ef database update --project backend/Bisp.Api.csproj
```

### Frontend

```bash
cd frontend
npm install
npm run dev       # Vite dev server (http://localhost:5173)
npm run build     # production build → dist/
npm run lint      # ESLint
```

### Tests

```bash
dotnet test Bisp.Api.Tests/Bisp.Api.Tests.csproj   # run all unit tests
```

Tests use xUnit with `FakeHttpMessageHandler` to stub HTTP calls. No database or network required.

## Configuration

Backend secrets must be set via `dotnet user-secrets` (run from `backend/`):

```bash
dotnet user-secrets set "Jwt:SigningKey" "<long-random-secret>"
dotnet user-secrets set "Igdb:ClientId" "<value>"
dotnet user-secrets set "Igdb:ClientSecret" "<value>"
dotnet user-secrets set "Stripe:SecretKey" "<value>"
dotnet user-secrets set "Stripe:WebhookSecret" "<value>"
dotnet user-secrets set "Smtp:Host" "<value>"
dotnet user-secrets set "Smtp:Port" "587"
dotnet user-secrets set "Smtp:User" "<value>"
dotnet user-secrets set "Smtp:Password" "<value>"
dotnet user-secrets set "Smtp:FromAddress" "<value>"
dotnet user-secrets set "Admin:Email" "<value>"
dotnet user-secrets set "Admin:Password" "<value>"
```

Non-secret defaults live in `backend/appsettings.json`. The connection string defaults to `Server=localhost;Database=BispApp;Trusted_Connection=True;TrustServerCertificate=True`.

**EF Core migrations have not been created yet** — the database schema must be initialized before first run.

## OpenAPI

Available at `http://localhost:5258/openapi/v1.json` in development (uses Scalar, not Swagger UI).
