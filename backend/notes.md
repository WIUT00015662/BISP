# BISP — Notes

Quick reference for local development, migrations, and running the apps.

## Prerequisites

- .NET 10 SDK installed
- `dotnet-ef` tool (install: `dotnet tool install --global dotnet-ef`)
- Node.js and npm for the frontend
- SQL Server (or any DB configured in `appsettings.json`)

## Backend (API)

From the repository root or any terminal:

- Build the backend:

```bash
dotnet build backend/Bisp.Api.csproj
```

- Run the backend (dev):

```bash
dotnet run --project backend/Bisp.Api.csproj
```

- EF Core migrations (create a new migration):

```bash
dotnet ef migrations add <Name> --project backend/Bisp.Api.csproj
```

- Apply migrations to the database:

```bash
dotnet ef database update --project backend/Bisp.Api.csproj
```

- Set required user-secrets (examples):

```bash
# run from repo root or specify --project
dotnet user-secrets set "Jwt:SigningKey" "<long-random-secret>" --project backend/Bisp.Api.csproj
dotnet user-secrets set "Admin:Email" "admin@example.com" --project backend/Bisp.Api.csproj
dotnet user-secrets set "Admin:Password" "ChangeMe123!" --project backend/Bisp.Api.csproj
```

Notes:
- Ensure the `dotnet-ef` tool is available in your PATH.
- If you change the `DbContext` or models, create a migration and apply it before running.

## Frontend (SPA)

From the `frontend` folder:

```bash
cd frontend
npm install
npm run dev
```

Build for production:

```bash
npm run build
```

The frontend dev server defaults to `http://localhost:5173` and backend is configured to allow CORS from that origin.

## Run both locally

Open two terminals:

Terminal 1 (backend):

```bash
dotnet run --project backend/Bisp.Api.csproj
```

Terminal 2 (frontend):

```bash
cd frontend
npm run dev
```

## Useful tips

- Connection string: update `backend/appsettings.json` or use environment variables.
- OpenAPI (dev) available at `http://localhost:5258/openapi/v1.json` when the API runs.
- If migrations fail, ensure the startup project is correct and the DB server is reachable.

---
Generated on: 2026-05-29
