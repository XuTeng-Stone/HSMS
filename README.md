# HSMS

HSMS is a Hospital Supply Management demo project focused on emergency-ready supply operations for OR/ER and general departments.  
It implements a practical end-to-end flow with inventory visibility, requisition lifecycle management, transfer logistics, and operational reporting.

## Tech stack

- .NET 10
- ASP.NET Core Minimal API
- Entity Framework Core
- SQLite
- xUnit integration tests
- Swagger UI

## Key capabilities

- Item catalog search with specification lookup
- Real-time inventory visibility by warehouse, lot, expiry, and location
- Standard and emergency requisition creation
- Approval/rejection workflow with role and policy checks
- FEFO-based pick-and-pack allocation
- Delivery task creation, accept, transit, arrival, and receipt confirmation
- Notification timeline and requisition status feed
- Inter-department stock transfer with completion and stock movement
- Return processing, wastage adjustment, and cycle-count reconciliation
- Low-stock and near-expiry monitoring
- KPI dashboard and operational reports
- Basic user and role management endpoints

For a complete functional breakdown, see `PROJECT_FEATURES.md`.

## Local run

```bash
dotnet restore
dotnet tool restore
dotnet run --project HSMS.Api
```

Open:

- UI: `http://localhost:5288/`
- Swagger: `http://localhost:5288/swagger`

On startup, the app applies migrations and seeds demo data into `HSMS.Api/data/hsms_demo.db`.

## Run tests

```bash
dotnet test HSMS.Api.Tests/HSMS.Api.Tests.csproj
```

## Common development commands

Add a migration after model changes:

```bash
dotnet tool run dotnet-ef migrations add <MigrationName> --project HSMS.Core --startup-project HSMS.Api
```

Apply migrations at runtime (already enabled by default in app startup):

```bash
dotnet run --project HSMS.Api
```
