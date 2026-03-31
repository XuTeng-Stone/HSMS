using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HSMS.Api.Data;
using HSMS.Core.Data;
using HSMS.Core.Entities;
using HSMS.Core.Enums;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

const int ReplenishThresholdPercent = 30;

var builder = WebApplication.CreateBuilder(args);
var dbDir = Path.Combine(builder.Environment.ContentRootPath, "data");
Directory.CreateDirectory(dbDir);
var dbPath = Path.Combine(dbDir, "hsms_demo.db");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
    await VirtualWarehouseLayoutBootstrap.EnsureAsync(db);
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/items", async ([FromQuery] string? q, [FromQuery] string? category, AppDbContext db) =>
{
    var query = db.Items.AsNoTracking();
    if (!string.IsNullOrWhiteSpace(q))
    {
        var term = q.Trim();
        var t = term.ToLower();
        var like = $"%{t}%";
        query = query.Where(i =>
            EF.Functions.Like(i.ItemName.ToLower(), like) ||
            (i.SpecificationText != null && EF.Functions.Like(i.SpecificationText.ToLower(), like)));
    }
    if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<ItemCategory>(category, true, out var cat))
        query = query.Where(i => i.Category == cat);
    var list = await query
        .OrderBy(i => i.ItemName)
        .Select(i => new ItemCatalogDto(
            i.ItemId,
            i.ItemName,
            i.Category.ToString(),
            i.UnitOfMeasure,
            i.SpecificationText,
            i.MinimumThreshold))
        .ToListAsync();
    return Results.Ok(list);
});

app.MapGet("/api/inventory/items/{itemId:guid}", async (
    Guid itemId,
    [FromQuery] Guid? warehouseId,
    AppDbContext db) =>
{
    var today = DateTime.UtcNow.Date;
    var item = await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.ItemId == itemId);
    if (item is null)
        return Results.NotFound();
    var recordsQuery = db.InventoryRecords.AsNoTracking()
        .Where(r => r.ItemId == itemId);
    if (warehouseId.HasValue)
        recordsQuery = recordsQuery.Where(r => r.WarehouseId == warehouseId.Value);
    var rows = await (
        from r in recordsQuery
        join w in db.Warehouses.AsNoTracking() on r.WarehouseId equals w.WarehouseId
        join sp in db.StoragePositions.AsNoTracking() on r.StoragePositionId equals sp.StoragePositionId into spg
        from sp in spg.DefaultIfEmpty()
        orderby w.Code, r.ExpiryDate
        select new InventoryRowDto(
            r.RecordId,
            r.WarehouseId,
            w.Code,
            w.DisplayName,
            r.QuantityOnHand,
            r.LocationBin,
            sp != null ? sp.RackCode : r.RackCode,
            sp != null ? sp.ShelfLevel : r.ShelfLevel,
            sp != null ? (int?)sp.MapPercentX : r.MapPercentX,
            sp != null ? (int?)sp.MapPercentY : r.MapPercentY,
            r.BatchLotNumber,
            r.ExpiryDate,
            r.QuantityOnHand > 0 && r.ExpiryDate >= today)
    ).ToListAsync();
    var groups = rows
        .GroupBy(x => new { x.WarehouseId, x.WarehouseCode, x.WarehouseName })
        .Select(g => new InventoryByWarehouseDto(
            g.Key.WarehouseId,
            g.Key.WarehouseCode,
            g.Key.WarehouseName,
            g.Sum(x => x.QuantityOnHand),
            g.Select(x => new InventoryRowDto(
                x.RecordId,
                x.WarehouseId,
                x.WarehouseCode,
                x.WarehouseName,
                x.QuantityOnHand,
                x.LocationBin,
                x.RackCode,
                x.ShelfLevel,
                x.MapPercentX,
                x.MapPercentY,
                x.BatchLotNumber,
                x.ExpiryDate,
                x.IsAvailable)).ToList()))
        .ToList();
    return Results.Ok(new ItemInventoryResponseDto(item.ItemId, item.ItemName, groups));
});

app.MapGet("/api/inventory/levels", async ([FromQuery] Guid? warehouseId, AppDbContext db) =>
{
    var profiles = db.ItemWarehouseProfiles.AsNoTracking()
        .Include(p => p.Item)
        .Include(p => p.Warehouse);
    var filtered = warehouseId.HasValue
        ? profiles.Where(p => p.WarehouseId == warehouseId.Value)
        : profiles;
    var list = await filtered.ToListAsync();
    var result = new List<InventoryLevelDto>();
    foreach (var p in list)
    {
        var onHand = await db.InventoryRecords
            .Where(r => r.ItemId == p.ItemId && r.WarehouseId == p.WarehouseId)
            .SumAsync(r => r.QuantityOnHand);
        var ceiling = p.SafetyStockCeiling;
        var fillPercent = ceiling <= 0 ? 0 : Math.Min(100, (int)Math.Floor(100.0 * onHand / ceiling));
        result.Add(new InventoryLevelDto(
            p.ItemId,
            p.Item.ItemName,
            p.WarehouseId,
            p.Warehouse.Code,
            onHand,
            ceiling,
            fillPercent,
            p.ReorderPoint,
            ReplenishThresholdPercent,
            fillPercent < ReplenishThresholdPercent));
    }
    return Results.Ok(result.OrderBy(x => x.ItemName).ThenBy(x => x.WarehouseCode));
});

app.MapPost("/api/stock-transfers", async ([FromBody] CreateStockTransferRequest body, AppDbContext db) =>
{
    if (body.Lines is null || body.Lines.Count == 0)
        return Results.BadRequest("At least one line is required.");
    var src = await db.Warehouses.FindAsync(body.SourceWarehouseId);
    var dst = await db.Warehouses.FindAsync(body.DestinationWarehouseId);
    if (src is null || dst is null)
        return Results.BadRequest("Unknown warehouse.");
    if (src.WarehouseId == dst.WarehouseId)
        return Results.BadRequest("Source and destination must differ.");
    if (!src.IsCentralHub || dst.IsCentralHub)
        return Results.BadRequest("Transfers must originate from the central hub to a satellite warehouse.");
    foreach (var line in body.Lines)
    {
        if (line.Quantity <= 0)
            return Results.BadRequest("Line quantity must be positive.");
        if (!await db.Items.AnyAsync(i => i.ItemId == line.ItemId))
            return Results.BadRequest($"Unknown item {line.ItemId}.");
    }
    var order = new StockTransferOrder
    {
        StockTransferOrderId = Guid.NewGuid(),
        SourceWarehouseId = src.WarehouseId,
        DestinationWarehouseId = dst.WarehouseId,
        RequestedAt = DateTime.UtcNow,
        Status = StockTransferOrderStatus.Submitted,
        RequestedByUserId = body.RequestedByUserId
    };
    foreach (var line in body.Lines)
    {
        order.Lines.Add(new StockTransferLine
        {
            StockTransferLineId = Guid.NewGuid(),
            StockTransferOrderId = order.StockTransferOrderId,
            ItemId = line.ItemId,
            Quantity = line.Quantity
        });
    }
    db.StockTransferOrders.Add(order);
    await db.SaveChangesAsync();
    return Results.Created($"/api/stock-transfers/{order.StockTransferOrderId}", new { order.StockTransferOrderId });
});

app.MapPost("/api/stock-transfers/{id:guid}/complete", async (Guid id, AppDbContext db) =>
{
    await using var tx = await db.Database.BeginTransactionAsync();
    var order = await db.StockTransferOrders
        .Include(o => o.Lines)
        .FirstOrDefaultAsync(o => o.StockTransferOrderId == id);
    if (order is null)
        return Results.NotFound();
    if (order.Status == StockTransferOrderStatus.Completed)
        return Results.BadRequest("Order already completed.");
    if (order.Status == StockTransferOrderStatus.Cancelled)
        return Results.BadRequest("Order is cancelled.");
    var recvSlot = await db.StoragePositions.AsNoTracking()
        .Where(p => p.WarehouseId == order.DestinationWarehouseId && p.PositionCode == "RECEIVING")
        .Select(p => new { p.StoragePositionId, p.RackCode, p.MapPercentX, p.MapPercentY })
        .FirstOrDefaultAsync();
    foreach (var line in order.Lines)
    {
        var remaining = line.Quantity;
        var sourceRows = await db.InventoryRecords
            .Where(r => r.ItemId == line.ItemId && r.WarehouseId == order.SourceWarehouseId && r.QuantityOnHand > 0)
            .OrderBy(r => r.ExpiryDate)
            .ToListAsync();
        var total = sourceRows.Sum(r => r.QuantityOnHand);
        if (total < remaining)
        {
            await tx.RollbackAsync();
            return Results.BadRequest(new { error = "Insufficient stock at source.", line.ItemId, requested = line.Quantity, available = total });
        }
        foreach (var row in sourceRows)
        {
            if (remaining <= 0)
                break;
            var take = Math.Min(remaining, row.QuantityOnHand);
            row.QuantityOnHand -= take;
            remaining -= take;
            var dest = await db.InventoryRecords.FirstOrDefaultAsync(r =>
                r.ItemId == line.ItemId &&
                r.WarehouseId == order.DestinationWarehouseId &&
                r.BatchLotNumber == row.BatchLotNumber &&
                r.ExpiryDate == row.ExpiryDate);
            if (dest is null)
            {
                db.InventoryRecords.Add(new InventoryRecord
                {
                    RecordId = Guid.NewGuid(),
                    ItemId = line.ItemId,
                    WarehouseId = order.DestinationWarehouseId,
                    BatchLotNumber = row.BatchLotNumber,
                    ExpiryDate = row.ExpiryDate,
                    QuantityOnHand = take,
                    LocationBin = "RECEIVING",
                    StoragePositionId = recvSlot?.StoragePositionId,
                    RackCode = recvSlot?.RackCode ?? "RCV",
                    ShelfLevel = 0,
                    MapPercentX = recvSlot?.MapPercentX ?? 88,
                    MapPercentY = recvSlot?.MapPercentY ?? 90
                });
            }
            else
            {
                dest.QuantityOnHand += take;
            }
        }
    }
    order.Status = StockTransferOrderStatus.Completed;
    order.CompletedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    await tx.CommitAsync();
    return Results.Ok(new { order.StockTransferOrderId, order.Status });
});

app.MapGet("/api/stock-transfers", async (AppDbContext db) =>
{
    var orders = await db.StockTransferOrders.AsNoTracking()
        .Include(o => o.SourceWarehouse)
        .Include(o => o.DestinationWarehouse)
        .Include(o => o.Lines)
        .ThenInclude(l => l.Item)
        .OrderByDescending(o => o.RequestedAt)
        .ToListAsync();
    var list = orders.Select(o => new StockTransferSummaryDto(
        o.StockTransferOrderId,
        o.SourceWarehouse.Code,
        o.DestinationWarehouse.Code,
        o.Status.ToString(),
        o.RequestedAt,
        o.CompletedAt,
        o.Lines.Select(l => new StockTransferLineDto(l.ItemId, l.Item.ItemName, l.Quantity)).ToList())).ToList();
    return Results.Ok(list);
});

app.MapGet("/api/stock-transfers/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var o = await db.StockTransferOrders.AsNoTracking()
        .Include(x => x.SourceWarehouse)
        .Include(x => x.DestinationWarehouse)
        .Include(x => x.Lines)
        .ThenInclude(l => l.Item)
        .FirstOrDefaultAsync(x => x.StockTransferOrderId == id);
    if (o is null)
        return Results.NotFound();
    var dto = new StockTransferDetailDto(
        o.StockTransferOrderId,
        o.SourceWarehouseId,
        o.SourceWarehouse.Code,
        o.DestinationWarehouseId,
        o.DestinationWarehouse.Code,
        o.Status.ToString(),
        o.RequestedAt,
        o.CompletedAt,
        o.RequestedByUserId,
        o.CompletedByUserId,
        o.Lines.Select(l => new StockTransferLineDto(l.ItemId, l.Item.ItemName, l.Quantity)).ToList());
    return Results.Ok(dto);
});

app.MapGet("/api/warehouses", async (AppDbContext db) =>
{
    var w = await db.Warehouses.AsNoTracking()
        .OrderBy(x => x.Code)
        .Select(x => new WarehouseDto(x.WarehouseId, x.Code, x.DisplayName, x.IsCentralHub))
        .ToListAsync();
    return Results.Ok(w);
});

app.MapGet("/api/warehouses/{warehouseId:guid}/virtual-layout", async (Guid warehouseId, AppDbContext db) =>
{
    var wh = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.WarehouseId == warehouseId);
    if (wh is null)
        return Results.NotFound();
    var zones = await db.WarehouseZones.AsNoTracking()
        .Where(z => z.WarehouseId == warehouseId)
        .OrderBy(z => z.SortOrder)
        .Select(z => new LayoutZoneDto(z.Label, z.SortOrder, z.RectX, z.RectY, z.RectW, z.RectH))
        .ToListAsync();
    var positions = await db.StoragePositions.AsNoTracking()
        .Where(p => p.WarehouseId == warehouseId)
        .OrderBy(p => p.PositionCode)
        .Select(p => new LayoutPositionDto(p.StoragePositionId, p.PositionCode, p.RackCode, p.ShelfLevel, p.MapPercentX, p.MapPercentY, p.AisleLabel))
        .ToListAsync();
    return Results.Ok(new VirtualWarehouseLayoutDto(wh.WarehouseId, wh.Code, wh.DisplayName, zones, positions));
});

app.MapGet("/api/inventory/items/{itemId:guid}/placement-map", async (Guid itemId, [FromQuery] Guid? warehouseId, AppDbContext db) =>
{
    var item = await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.ItemId == itemId);
    if (item is null)
        return Results.NotFound();
    var whIds = await db.InventoryRecords.AsNoTracking()
        .Where(r => r.ItemId == itemId && r.QuantityOnHand > 0 && (!warehouseId.HasValue || r.WarehouseId == warehouseId.Value))
        .Select(r => r.WarehouseId)
        .Distinct()
        .OrderBy(x => x)
        .ToListAsync();
    var maps = new List<WarehousePlacementMapDto>();
    foreach (var wid in whIds)
    {
        var wh = await db.Warehouses.AsNoTracking().FirstAsync(w => w.WarehouseId == wid);
        var zones = await db.WarehouseZones.AsNoTracking()
            .Where(z => z.WarehouseId == wid)
            .OrderBy(z => z.SortOrder)
            .Select(z => new LayoutZoneDto(z.Label, z.SortOrder, z.RectX, z.RectY, z.RectW, z.RectH))
            .ToListAsync();
        var markers = await (
            from r in db.InventoryRecords.AsNoTracking()
            where r.ItemId == itemId && r.QuantityOnHand > 0 && r.WarehouseId == wid
            join sp in db.StoragePositions.AsNoTracking() on r.StoragePositionId equals sp.StoragePositionId into spj
            from sp in spj.DefaultIfEmpty()
            select new PlacementMarkerDto(
                r.RecordId,
                sp != null ? sp.StoragePositionId : null,
                sp != null ? sp.PositionCode : r.LocationBin,
                sp != null ? sp.RackCode : r.RackCode,
                sp != null ? sp.ShelfLevel : r.ShelfLevel,
                sp != null ? sp.MapPercentX : (r.MapPercentX ?? 50),
                sp != null ? sp.MapPercentY : (r.MapPercentY ?? 50),
                r.LocationBin,
                r.QuantityOnHand,
                r.BatchLotNumber,
                r.ExpiryDate)
        ).ToListAsync();
        maps.Add(new WarehousePlacementMapDto(wid, wh.Code, wh.DisplayName, zones, markers));
    }
    return Results.Ok(new ItemPlacementMapResponseDto(item.ItemId, item.ItemName, maps));
});

app.Run();

internal sealed record ItemCatalogDto(Guid ItemId, string ItemName, string Category, string UnitOfMeasure, string? SpecificationText, int MinimumThreshold);

internal sealed record InventoryRowDto(
    Guid RecordId,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    int QuantityOnHand,
    string LocationBin,
    string RackCode,
    int ShelfLevel,
    int? MapPercentX,
    int? MapPercentY,
    string BatchLotNumber,
    DateTime ExpiryDate,
    bool IsAvailable);

internal sealed record InventoryByWarehouseDto(
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    int TotalOnHand,
    IReadOnlyList<InventoryRowDto> Records);

internal sealed record ItemInventoryResponseDto(Guid ItemId, string ItemName, IReadOnlyList<InventoryByWarehouseDto> ByWarehouse);

internal sealed record InventoryLevelDto(
    Guid ItemId,
    string ItemName,
    Guid WarehouseId,
    string WarehouseCode,
    int OnHand,
    int SafetyStockCeiling,
    int FillPercent,
    int ReorderPoint,
    int ReplenishThresholdPercent,
    bool NeedsReplenishment);

internal sealed record CreateStockTransferRequest(
    [Required] Guid SourceWarehouseId,
    [Required] Guid DestinationWarehouseId,
    [Required] IReadOnlyList<CreateStockTransferLineRequest> Lines,
    Guid? RequestedByUserId);

internal sealed record CreateStockTransferLineRequest([Required] Guid ItemId, [Range(1, int.MaxValue)] int Quantity);

internal sealed record StockTransferLineDto(Guid ItemId, string ItemName, int Quantity);

internal sealed record StockTransferSummaryDto(
    Guid StockTransferOrderId,
    string SourceWarehouseCode,
    string DestinationWarehouseCode,
    string Status,
    DateTime RequestedAt,
    DateTime? CompletedAt,
    IReadOnlyList<StockTransferLineDto> Lines);

internal sealed record StockTransferDetailDto(
    Guid StockTransferOrderId,
    Guid SourceWarehouseId,
    string SourceWarehouseCode,
    Guid DestinationWarehouseId,
    string DestinationWarehouseCode,
    string Status,
    DateTime RequestedAt,
    DateTime? CompletedAt,
    Guid? RequestedByUserId,
    Guid? CompletedByUserId,
    IReadOnlyList<StockTransferLineDto> Lines);

internal sealed record WarehouseDto(Guid WarehouseId, string Code, string DisplayName, bool IsCentralHub);

internal sealed record LayoutZoneDto(string Label, int SortOrder, int RectX, int RectY, int RectW, int RectH);

internal sealed record LayoutPositionDto(Guid StoragePositionId, string PositionCode, string RackCode, int ShelfLevel, int MapPercentX, int MapPercentY, string? AisleLabel);

internal sealed record VirtualWarehouseLayoutDto(
    Guid WarehouseId,
    string Code,
    string DisplayName,
    IReadOnlyList<LayoutZoneDto> Zones,
    IReadOnlyList<LayoutPositionDto> Positions);

internal sealed record PlacementMarkerDto(
    Guid RecordId,
    Guid? StoragePositionId,
    string PositionCode,
    string RackCode,
    int ShelfLevel,
    int MapPercentX,
    int MapPercentY,
    string LocationBin,
    int QuantityOnHand,
    string BatchLotNumber,
    DateTime ExpiryDate);

internal sealed record WarehousePlacementMapDto(
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    IReadOnlyList<LayoutZoneDto> Zones,
    IReadOnlyList<PlacementMarkerDto> Markers);

internal sealed record ItemPlacementMapResponseDto(
    Guid ItemId,
    string ItemName,
    IReadOnlyList<WarehousePlacementMapDto> WarehouseMaps);

public partial class Program;
