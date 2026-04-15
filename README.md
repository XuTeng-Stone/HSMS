# HSMS

HSMS is a Hospital Supply Management demo focused on OR/ER-ready supply operations and general wards.  
It combines a **browser UI**, a **REST API**, and **SQLite** so you can demo catalog lookup, live stock, requisition-to-receipt flow, transfers, and reporting without external services.

## Tech stack

- .NET 10
- ASP.NET Core Minimal API
- Entity Framework Core + SQLite
- Static web UI (`HSMS.Api/wwwroot`)
- Swagger UI
- xUnit integration tests (`HSMS.Api.Tests`)

## What you can demo in the UI

Open `http://localhost:5288/` after starting the API.

| Tab | Purpose |
| --- | --- |
| **Catalog** | Search items by name/spec/category; jump to stock or open placement map |
| **Stock & locations** | Bin-level quantity, lot, expiry; virtual **placement map** |
| **Warehouse levels** | Fill vs safety ceiling and low-fill alerts |
| **Transfers** | Create hub-to-satellite transfer orders and **Complete** to move stock (FEFO at source) |
| **Requisitions** | Create **standard** or **emergency** requests, then **Approve → Pick & pack → Create delivery → Courier accept → Arrived → Confirm receipt**; load **notifications** / **timeline** |

Demo users and inventory are aligned with the API tests (fixed GUIDs). If you use an **old local database** from before demo users existed, restart the API once: startup runs `EnsureDemoUsersAsync` so requesters always exist for the Requisitions UI.

## Local run

```bash
dotnet restore
dotnet tool restore
dotnet run --project HSMS.Api
```

- **UI:** `http://localhost:5288/`
- **Swagger:** `http://localhost:5288/swagger`
- **Database file:** `HSMS.Api/data/hsms_demo.db` (migrations + seed + demo user repair on startup)

## Run tests

```bash
dotnet test HSMS.Api.Tests/HSMS.Api.Tests.csproj
```

## Documentation

- **`PROJECT_FEATURES.md`** — module list, main endpoints, and known boundaries (auth, audit, push notifications).

## Common development commands

Add a migration after model changes:

```bash
dotnet tool run dotnet-ef migrations add <MigrationName> --project HSMS.Core --startup-project HSMS.Api
```

Runtime already applies migrations on startup when you run the API.
