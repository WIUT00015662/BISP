# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

BISP is a game price aggregator — a full-stack app that tracks game prices across stores (Steam, GOG, Epic), manages wishlists, and notifies users of discounts. The project is at an early scaffold/PoC stage: many services exist as stubs awaiting implementation.

## Architecture

Two independently running processes:

- **Backend**: ASP.NET Core Web API (.NET 10) — `backend/`
- **Frontend**: React 19 SPA (Vite 8) — `frontend/`

The frontend calls the backend directly (no proxy). CORS is configured to allow `http://localhost:5173`.

### Backend structure

- `Program.cs` — composition root; registers all services, middleware, seeding
- `Controllers/` — only `AdminController` (role-gated aggregation trigger) and `HealthController` exist; auth endpoints are not yet implemented
- `Data/AppDbContext.cs` — EF Core 10, extends `IdentityDbContext<ApplicationUser>`, SQL Server
- `Models/` — EF Core entities: `Game`, `Store`, `GameStorePrice`, `ExternalGameId`, `WishlistItem`, `UserSubscription`, `NotificationLog`
- `Options/` — strongly-typed config classes bound to `appsettings.json` sections via `IOptions<T>`
- `Services/AggregationWorker.cs` — `BackgroundService` using `PeriodicTimer`; fires `AggregationService.RunAsync()` every `Aggregation:IntervalHours` (default: 10)
- `Services/AdminSeeder.cs` — seeds `Admin` role and user at startup

### Auth model

ASP.NET Core Identity + JWT Bearer. Tokens use issuer `Bisp.Api`, audience `Bisp.Spa`. Email confirmation is required. `[Authorize(Roles = "Admin")]` guards admin routes.

### Known stubs (not yet implemented)

- `AggregationService.RunAsync()` — Steam/GOG/Epic ingestion + IGDB metadata mapping
- `NotificationService.CheckWishlistAsync()` — wishlist discount threshold checks
- `EmailSender.SendAsync()` — logs only; no SMTP client library added yet
- All `/api/auth/*` endpoints (register, login, confirm-email)
- Frontend `App.jsx` — still the Vite scaffold template

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
