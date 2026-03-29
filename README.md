# HSMS

Hospital Supply Management demo: catalog search, real-time stock by warehouse, safety-ceiling fill levels, and central-to-satellite stock transfers (REST API + web UI + Swagger).

## How to run

Clone the repository, then from the repo root install dependencies and the EF Core tool, start the API, and open the site in a browser. The app uses SQLite; on first run it applies migrations, seeds demo warehouses and items, and writes the database under `HSMS.Api/data/hsms_demo.db`.

```bash
git clone https://github.com/XuTeng-Stone/HSMS.git
cd HSMS
dotnet restore
dotnet tool restore
dotnet run --project HSMS.Api
```

Then open **http://localhost:5288/** for the UI, or **http://localhost:5288/swagger** for the API docs. To add database migrations after model changes:

```bash
dotnet tool run dotnet-ef migrations add <MigrationName> --project HSMS.Core --startup-project HSMS.Api
```
