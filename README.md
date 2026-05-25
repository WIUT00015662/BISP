# BISP Game Price Aggregator (PoC)

Proof-of-concept game price aggregator built with ASP.NET Core Web API + React (Vite). It ingests Steam Top Sellers, maps games with IGDB External Games, and aggregates prices from Steam, GOG, and Epic. Wishlist notifications are email-based and gated by Stripe subscription.

## Requirements

- .NET SDK (net10.0)
- Node.js + npm
- SQL Server (local instance)

## Configuration

Backend configuration lives in [backend/appsettings.json](backend/appsettings.json) and user-secrets for sensitive values.

Recommended user-secrets (run inside backend project folder):

```
dotnet user-secrets set "Jwt:SigningKey" "REPLACE_WITH_LONG_RANDOM_SECRET"
dotnet user-secrets set "Igdb:ClientId" "YOUR_IGDB_CLIENT_ID"
dotnet user-secrets set "Igdb:ClientSecret" "YOUR_IGDB_CLIENT_SECRET"
dotnet user-secrets set "Stripe:SecretKey" "YOUR_STRIPE_TEST_SECRET"
dotnet user-secrets set "Stripe:WebhookSecret" "YOUR_STRIPE_WEBHOOK_SECRET"
dotnet user-secrets set "Smtp:Host" "smtp.example.com"
dotnet user-secrets set "Smtp:Port" "587"
dotnet user-secrets set "Smtp:User" "your_user"
dotnet user-secrets set "Smtp:Password" "your_password"
dotnet user-secrets set "Smtp:FromAddress" "no-reply@example.com"
dotnet user-secrets set "Admin:Email" "admin@example.com"
dotnet user-secrets set "Admin:Password" "ChangeMe123!"
```

## Run Backend

```
dotnet run --project backend/Bisp.Api.csproj
```

## Run Frontend

```
cd frontend
npm install
npm run dev
```

## Notes

- Database migrations are not created yet; add EF Core migrations once the schema is finalized.
- If a store API is unavailable, reduce the PoC to two stores.
- Wishlist discount checks are intended to run every 10 seconds and send email only when the threshold is met.
