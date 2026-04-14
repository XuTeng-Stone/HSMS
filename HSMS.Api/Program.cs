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
app.MapGet("/favicon.ico", () => Results.NoContent());

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
    if (body.RequestedByUserId.HasValue)
    {
        var requester = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == body.RequestedByUserId.Value);
        if (requester is null || !requester.IsActive)
            return Results.BadRequest("Requester user is invalid or inactive.");
        if (requester.SystemRole is not (SystemRole.InventoryManager or SystemRole.Admin))
            return Results.BadRequest("Only inventory manager or admin can create transfer orders.");
    }
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
    return Results.Created($"/api/stock-transfers/{order.StockTransferOrderId}", new { order.StockTransferOrderId, body.Priority, body.Justification });
});

app.MapPost("/api/stock-transfers/{id:guid}/complete", async (Guid id, [FromBody] CompleteStockTransferRequest? body, AppDbContext db) =>
{
    await using var tx = await db.Database.BeginTransactionAsync();
    if (body?.CompletedByUserId is Guid completedByUserId)
    {
        var operatorUser = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == completedByUserId);
        if (operatorUser is null || !operatorUser.IsActive)
            return Results.BadRequest("CompletedByUserId is invalid or inactive.");
        if (operatorUser.SystemRole is not (SystemRole.InventoryManager or SystemRole.Admin))
            return Results.BadRequest("Only inventory manager or admin can complete transfer orders.");
    }
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
    order.CompletedByUserId = body?.CompletedByUserId;
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

// Requisition creation endpoints for standard and emergency requests.
app.MapPost("/api/requisitions/standard", async ([FromBody] CreateStandardRequisitionRequest body, AppDbContext db) =>
{
    var validationError = await ValidateRequisitionRequestAsync(body.RequestedById, body.DeliveryLocation, body.Lines, db);
    if (validationError is not null)
        return validationError;

    var requisition = new StandardRequisition
    {
        RequisitionId = Guid.NewGuid(),
        RequestedById = body.RequestedById,
        RequestDate = DateTime.UtcNow,
        Status = RequisitionStatus.Pending,
        DeliveryLocation = body.DeliveryLocation.Trim(),
        TargetDeliveryWindow = body.TargetDeliveryWindow.Trim()
    };
    requisition.RequisitionLineItems = body.Lines
        .Select(line => new RequisitionLineItem
        {
            LineItemId = Guid.NewGuid(),
            RequisitionId = requisition.RequisitionId,
            ItemId = line.ItemId,
            RequestedQuantity = line.Quantity,
            FulfilledQuantity = 0
        })
        .ToList();

    db.Requisitions.Add(requisition);
    await db.SaveChangesAsync();
    return Results.Created($"/api/requisitions/{requisition.RequisitionId}", new { requisition.RequisitionId, Type = "Standard" });
});

app.MapPost("/api/requisitions/emergency", async ([FromBody] CreateEmergencyRequisitionRequest body, AppDbContext db) =>
{
    var validationError = await ValidateRequisitionRequestAsync(body.RequestedById, body.DeliveryLocation, body.Lines, db);
    if (validationError is not null)
        return validationError;
    if (string.IsNullOrWhiteSpace(body.JustificationCode))
        return Results.BadRequest("JustificationCode is required for emergency requisitions.");

    var requisition = new EmergencyRequisition
    {
        RequisitionId = Guid.NewGuid(),
        RequestedById = body.RequestedById,
        RequestDate = DateTime.UtcNow,
        Status = RequisitionStatus.Pending,
        DeliveryLocation = body.DeliveryLocation.Trim(),
        JustificationCode = body.JustificationCode.Trim()
    };
    requisition.RequisitionLineItems = body.Lines
        .Select(line => new RequisitionLineItem
        {
            LineItemId = Guid.NewGuid(),
            RequisitionId = requisition.RequisitionId,
            ItemId = line.ItemId,
            RequestedQuantity = line.Quantity,
            FulfilledQuantity = 0
        })
        .ToList();

    db.Requisitions.Add(requisition);
    await db.SaveChangesAsync();
    return Results.Created($"/api/requisitions/{requisition.RequisitionId}", new { requisition.RequisitionId, Type = "Emergency" });
});

// Queue view for inventory managers; emergency requests are sorted first.
app.MapGet("/api/requisitions", async (
    [FromQuery] string? status,
    [FromQuery] bool queueOrder,
    AppDbContext db) =>
{
    var requisitions = db.Requisitions.AsNoTracking()
        .Include(r => r.RequestedBy)
        .Include(r => r.RequisitionLineItems)
        .ThenInclude(l => l.Item)
        .AsQueryable();

    if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RequisitionStatus>(status, true, out var parsedStatus))
        requisitions = requisitions.Where(r => r.Status == parsedStatus);

    var data = await requisitions.ToListAsync();
    var mapped = data.Select(r => new RequisitionSummaryDto(
        r.RequisitionId,
        r is EmergencyRequisition,
        r.Status.ToString(),
        r.RequestDate,
        r.RequestedById,
        r.RequestedBy.FullName,
        r.DeliveryLocation,
        r.RequisitionLineItems.Sum(x => x.RequestedQuantity),
        r.RequisitionLineItems.Sum(x => x.FulfilledQuantity),
        r.RequisitionLineItems.Select(x => new RequisitionItemDto(x.ItemId, x.Item.ItemName, x.RequestedQuantity, x.FulfilledQuantity)).ToList()))
        .ToList();

    if (queueOrder)
    {
        mapped = mapped
            .OrderByDescending(x => x.IsEmergency)
            .ThenBy(x => x.Status == RequisitionStatus.Pending.ToString() ? 0 : 1)
            .ThenBy(x => x.RequestDate)
            .ToList();
    }
    else
    {
        mapped = mapped
            .OrderByDescending(x => x.RequestDate)
            .ToList();
    }

    return Results.Ok(mapped);
});

app.MapGet("/api/requisitions/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var requisition = await db.Requisitions.AsNoTracking()
        .Include(r => r.RequestedBy)
        .Include(r => r.RequisitionLineItems)
        .ThenInclude(l => l.Item)
        .Include(r => r.PickLists)
        .Include(r => r.DeliveryTask!)
        .ThenInclude(d => d.AssignedTo)
        .FirstOrDefaultAsync(r => r.RequisitionId == id);
    if (requisition is null)
        return Results.NotFound();

    return Results.Ok(new RequisitionDetailDto(
        requisition.RequisitionId,
        requisition is EmergencyRequisition,
        requisition.Status.ToString(),
        requisition.RequestDate,
        requisition.RequestedBy.FullName,
        requisition.DeliveryLocation,
        requisition.PickLists.OrderByDescending(p => p.CreationTimestamp).Select(p => new PickListDto(p.PickListId, p.PickStatus.ToString(), p.CreationTimestamp, p.GeneratedById)).ToList(),
        requisition.DeliveryTask is null
            ? null
            : new DeliveryTaskDto(
                requisition.DeliveryTask.TaskId,
                requisition.DeliveryTask.DeliveryStatus.ToString(),
                requisition.DeliveryTask.DispatchTime,
                requisition.DeliveryTask.AssignedToId,
                requisition.DeliveryTask.AssignedTo?.FullName),
        requisition.RequisitionLineItems.Select(x => new RequisitionItemDto(x.ItemId, x.Item.ItemName, x.RequestedQuantity, x.FulfilledQuantity)).ToList()));
});

app.MapPost("/api/requisitions/{id:guid}/approve", async (Guid id, [FromBody] ApproveRequisitionRequest body, AppDbContext db) =>
{
    var requisition = await db.Requisitions
        .Include(r => r.RequisitionLineItems)
        .FirstOrDefaultAsync(r => r.RequisitionId == id);
    if (requisition is null)
        return Results.NotFound();
    if (requisition.Status != RequisitionStatus.Pending)
        return Results.BadRequest("Only pending requisitions can be approved.");
    var approver = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == body.ApprovedByUserId);
    if (approver is null || !approver.IsActive)
        return Results.BadRequest("Approver user does not exist or is inactive.");
    if (approver.SystemRole is not (SystemRole.InventoryManager or SystemRole.Admin))
        return Results.BadRequest("Only inventory manager or admin can approve requisitions.");
    var hasHighValueItem = await (
        from li in db.RequisitionLineItems.AsNoTracking()
        join i in db.Items.AsNoTracking() on li.ItemId equals i.ItemId
        where li.RequisitionId == requisition.RequisitionId && i.Category == ItemCategory.HighValueImplants
        select li.LineItemId
    ).AnyAsync();
    if (hasHighValueItem && string.IsNullOrWhiteSpace(body.ApprovalNote))
        return Results.BadRequest("ApprovalNote is required for requisitions containing high-value implants.");

    requisition.Status = RequisitionStatus.Approved;
    await db.SaveChangesAsync();
    return Results.Ok(new { requisition.RequisitionId, Status = requisition.Status.ToString(), body.ApprovalNote });
});

app.MapPost("/api/requisitions/{id:guid}/reject", async (Guid id, [FromBody] RejectRequisitionRequest body, AppDbContext db) =>
{
    var requisition = await db.Requisitions.FirstOrDefaultAsync(r => r.RequisitionId == id);
    if (requisition is null)
        return Results.NotFound();
    if (requisition.Status != RequisitionStatus.Pending && requisition.Status != RequisitionStatus.Approved)
        return Results.BadRequest("Only pending or approved requisitions can be rejected.");
    var rejector = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == body.RejectedByUserId);
    if (rejector is null || !rejector.IsActive)
        return Results.BadRequest("Rejector user does not exist or is inactive.");
    if (rejector.SystemRole is not (SystemRole.InventoryManager or SystemRole.Admin))
        return Results.BadRequest("Only inventory manager or admin can reject requisitions.");
    if (string.IsNullOrWhiteSpace(body.Reason))
        return Results.BadRequest("Reason is required when rejecting requisitions.");

    requisition.Status = RequisitionStatus.Cancelled;
    await db.SaveChangesAsync();
    return Results.Ok(new { requisition.RequisitionId, Status = requisition.Status.ToString(), body.Reason });
});

// Picking endpoint performs FEFO allocation and scan-style verification.
app.MapPost("/api/requisitions/{id:guid}/pick-and-pack", async (Guid id, [FromBody] PickAndPackRequest body, AppDbContext db) =>
{
    await using var tx = await db.Database.BeginTransactionAsync();
    var requisition = await db.Requisitions
        .Include(r => r.RequisitionLineItems)
        .FirstOrDefaultAsync(r => r.RequisitionId == id);
    if (requisition is null)
        return Results.NotFound();
    if (requisition.Status != RequisitionStatus.Approved)
        return Results.BadRequest("Only approved requisitions can be picked.");
    var picker = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == body.PickedByUserId);
    if (picker is null || !picker.IsActive)
        return Results.BadRequest("Picker user does not exist or is inactive.");
    if (picker.SystemRole is not (SystemRole.InventoryManager or SystemRole.Admin))
        return Results.BadRequest("Only inventory manager or admin can perform pick-and-pack.");

    var shortages = new List<PickShortageDto>();
    foreach (var line in requisition.RequisitionLineItems)
    {
        var remaining = line.RequestedQuantity;
        var stockRows = await db.InventoryRecords
            .Where(r => r.ItemId == line.ItemId && r.QuantityOnHand > 0)
            .OrderBy(r => r.ExpiryDate)
            .ToListAsync();
        foreach (var stockRow in stockRows)
        {
            if (remaining <= 0)
                break;
            var allocated = Math.Min(remaining, stockRow.QuantityOnHand);
            stockRow.QuantityOnHand -= allocated;
            line.FulfilledQuantity += allocated;
            remaining -= allocated;
        }
        if (remaining > 0)
        {
            shortages.Add(new PickShortageDto(line.ItemId, line.RequestedQuantity, line.FulfilledQuantity));
        }
    }

    var pickList = new PickList
    {
        PickListId = Guid.NewGuid(),
        RequisitionId = requisition.RequisitionId,
        GeneratedById = body.PickedByUserId,
        CreationTimestamp = DateTime.UtcNow,
        PickStatus = PickStatus.Packed
    };
    db.PickLists.Add(pickList);

    if (shortages.Count > 0)
    {
        await tx.RollbackAsync();
        return Results.BadRequest(new { error = "Insufficient stock for one or more line items.", shortages });
    }

    await db.SaveChangesAsync();
    await tx.CommitAsync();
    return Results.Ok(new { requisition.RequisitionId, PickListId = pickList.PickListId, pickList.PickStatus });
});

app.MapPost("/api/requisitions/{id:guid}/delivery-task", async (Guid id, [FromBody] CreateDeliveryTaskRequest body, AppDbContext db) =>
{
    var requisition = await db.Requisitions
        .Include(r => r.DeliveryTask)
        .Include(r => r.PickLists)
        .FirstOrDefaultAsync(r => r.RequisitionId == id);
    if (requisition is null)
        return Results.NotFound();
    if (requisition.Status != RequisitionStatus.Approved)
        return Results.BadRequest("Requisition must be approved before delivery.");
    if (!requisition.PickLists.Any(p => p.PickStatus == PickStatus.Packed))
        return Results.BadRequest("Requisition must be packed before creating a delivery task.");
    if (requisition.DeliveryTask is not null)
        return Results.BadRequest("Delivery task already exists.");
    var courier = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == body.AssignedToId);
    if (courier is null || !courier.IsActive || courier.SystemRole != SystemRole.LogisticsStaff)
        return Results.BadRequest("Assigned user must be an active logistics staff.");

    var task = new DeliveryTask
    {
        TaskId = Guid.NewGuid(),
        RequisitionId = requisition.RequisitionId,
        AssignedToId = body.AssignedToId,
        DispatchTime = null,
        DeliveryStatus = DeliveryStatus.Pending
    };
    db.DeliveryTasks.Add(task);
    await db.SaveChangesAsync();
    return Results.Created($"/api/delivery-tasks/{task.TaskId}", new { task.TaskId, Status = task.DeliveryStatus.ToString() });
});

app.MapPost("/api/delivery-tasks/{taskId:guid}/accept", async (Guid taskId, AppDbContext db) =>
{
    var task = await db.DeliveryTasks
        .Include(t => t.Requisition)
        .FirstOrDefaultAsync(t => t.TaskId == taskId);
    if (task is null)
        return Results.NotFound();
    if (task.DeliveryStatus != DeliveryStatus.Pending)
        return Results.BadRequest("Only pending tasks can be accepted.");

    task.DeliveryStatus = DeliveryStatus.InTransit;
    task.DispatchTime = DateTime.UtcNow;
    task.Requisition.Status = RequisitionStatus.InTransit;
    await db.SaveChangesAsync();
    return Results.Ok(new { task.TaskId, DeliveryStatus = task.DeliveryStatus.ToString(), RequisitionStatus = task.Requisition.Status.ToString() });
});

app.MapPost("/api/delivery-tasks/{taskId:guid}/arrive", async (Guid taskId, AppDbContext db) =>
{
    var task = await db.DeliveryTasks.FirstOrDefaultAsync(t => t.TaskId == taskId);
    if (task is null)
        return Results.NotFound();
    if (task.DeliveryStatus != DeliveryStatus.InTransit)
        return Results.BadRequest("Only in-transit tasks can be marked as arrived.");

    task.DeliveryStatus = DeliveryStatus.Arrived;
    await db.SaveChangesAsync();
    return Results.Ok(new { task.TaskId, Status = task.DeliveryStatus.ToString() });
});

app.MapPost("/api/requisitions/{id:guid}/confirm-receipt", async (Guid id, AppDbContext db) =>
{
    var requisition = await db.Requisitions
        .Include(r => r.DeliveryTask)
        .Include(r => r.RequisitionLineItems)
        .FirstOrDefaultAsync(r => r.RequisitionId == id);
    if (requisition is null)
        return Results.NotFound();
    if (requisition.DeliveryTask is null || requisition.DeliveryTask.DeliveryStatus != DeliveryStatus.Arrived)
        return Results.BadRequest("Delivery must be arrived before confirmation.");

    requisition.Status = RequisitionStatus.Completed;
    await db.SaveChangesAsync();
    return Results.Ok(new
    {
        requisition.RequisitionId,
        Status = requisition.Status.ToString(),
        RequestedTotal = requisition.RequisitionLineItems.Sum(x => x.RequestedQuantity),
        FulfilledTotal = requisition.RequisitionLineItems.Sum(x => x.FulfilledQuantity)
    });
});

app.MapGet("/api/requisitions/{id:guid}/timeline", async (Guid id, AppDbContext db) =>
{
    var requisition = await db.Requisitions.AsNoTracking()
        .Include(r => r.PickLists)
        .Include(r => r.DeliveryTask)
        .FirstOrDefaultAsync(r => r.RequisitionId == id);
    if (requisition is null)
        return Results.NotFound();

    var events = new List<RequisitionTimelineEventDto>
    {
        new("requested", requisition.RequestDate, "Requisition submitted."),
        new("status", requisition.RequestDate, $"Current status: {requisition.Status}.")
    };
    var latestPick = requisition.PickLists.OrderByDescending(p => p.CreationTimestamp).FirstOrDefault();
    if (latestPick is not null)
        events.Add(new("packed", latestPick.CreationTimestamp, $"Pick list packed ({latestPick.PickListId})."));
    if (requisition.DeliveryTask is not null)
    {
        if (requisition.DeliveryTask.DispatchTime.HasValue)
            events.Add(new("in-transit", requisition.DeliveryTask.DispatchTime.Value, "Courier accepted and departed."));
        if (requisition.DeliveryTask.DeliveryStatus == DeliveryStatus.Arrived)
            events.Add(new("arrived", DateTime.UtcNow, "Delivery reached destination; awaiting receipt confirmation."));
    }
    return Results.Ok(events.OrderBy(e => e.Timestamp));
});

app.MapGet("/api/items/{itemId:guid}/substitutions", async (Guid itemId, AppDbContext db) =>
{
    var source = await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.ItemId == itemId);
    if (source is null)
        return Results.NotFound();
    var today = DateTime.UtcNow.Date;
    var substitutions = await (
        from i in db.Items.AsNoTracking()
        where i.ItemId != itemId && i.Category == source.Category
        let total = db.InventoryRecords.Where(r => r.ItemId == i.ItemId && r.QuantityOnHand > 0 && r.ExpiryDate >= today).Sum(r => r.QuantityOnHand)
        where total > 0
        orderby total descending, i.ItemName
        select new SubstitutionDto(i.ItemId, i.ItemName, i.SpecificationText, total)
    ).Take(5).ToListAsync();
    return Results.Ok(new ItemSubstitutionResponseDto(source.ItemId, source.ItemName, substitutions));
});

app.MapGet("/api/replenishment/suggestions", async (AppDbContext db) =>
{
    var profiles = await db.ItemWarehouseProfiles.AsNoTracking()
        .Include(p => p.Item)
        .Include(p => p.Warehouse)
        .ToListAsync();
    var suggestions = new List<ReplenishmentSuggestionDto>();
    foreach (var profile in profiles)
    {
        var onHand = await db.InventoryRecords.AsNoTracking()
            .Where(r => r.ItemId == profile.ItemId && r.WarehouseId == profile.WarehouseId)
            .SumAsync(r => r.QuantityOnHand);
        if (onHand < profile.ReorderPoint)
        {
            suggestions.Add(new ReplenishmentSuggestionDto(
                profile.ItemId,
                profile.Item.ItemName,
                profile.WarehouseId,
                profile.Warehouse.Code,
                onHand,
                profile.ReorderPoint,
                Math.Max(profile.ReorderPoint - onHand, profile.SafetyStockCeiling - onHand)));
        }
    }
    return Results.Ok(suggestions.OrderByDescending(x => x.SuggestedQty).ThenBy(x => x.ItemName));
});

app.MapGet("/api/dashboard/kpis", async (AppDbContext db) =>
{
    var last30Days = DateTime.UtcNow.AddDays(-30);
    var requisitions = db.Requisitions.AsNoTracking().Where(r => r.RequestDate >= last30Days);
    var total = await requisitions.CountAsync();
    var completed = await requisitions.CountAsync(r => r.Status == RequisitionStatus.Completed);
    var cancelled = await requisitions.CountAsync(r => r.Status == RequisitionStatus.Cancelled);
    var inTransit = await requisitions.CountAsync(r => r.Status == RequisitionStatus.InTransit);
    var pending = await requisitions.CountAsync(r => r.Status == RequisitionStatus.Pending || r.Status == RequisitionStatus.Approved);
    var profiles = await db.ItemWarehouseProfiles.AsNoTracking().ToListAsync();
    var lowStockCount = 0;
    foreach (var profile in profiles)
    {
        var onHand = await db.InventoryRecords.AsNoTracking()
            .Where(r => r.ItemId == profile.ItemId && r.WarehouseId == profile.WarehouseId)
            .SumAsync(r => r.QuantityOnHand);
        if (onHand < profile.ReorderPoint)
            lowStockCount++;
    }
    var requisitionKpiRows = await db.Requisitions.AsNoTracking()
        .Where(r => r.RequestDate >= last30Days)
        .Include(r => r.RequestedBy)
        .Include(r => r.PickLists)
        .Select(r => new
        {
            r.RequisitionId,
            r.RequestDate,
            r.Status,
            IsEmergency = r is EmergencyRequisition,
            Department = r.RequestedBy.Department,
            PackedAt = r.PickLists.OrderBy(p => p.CreationTimestamp).Select(p => (DateTime?)p.CreationTimestamp).FirstOrDefault()
        })
        .ToListAsync();
    var emergencyRows = requisitionKpiRows.Where(x => x.IsEmergency).ToList();
    var emergencyCompletedWithin30Min = emergencyRows.Count(x =>
        x.Status == RequisitionStatus.Completed &&
        x.PackedAt.HasValue &&
        (x.PackedAt.Value - x.RequestDate).TotalMinutes <= 30);
    var orErRows = requisitionKpiRows.Where(x => x.Department == "OR" || x.Department == "ER").ToList();
    var orErCompleted = orErRows.Count(x => x.Status == RequisitionStatus.Completed);

    return Results.Ok(new KpiDashboardDto(
        total,
        completed,
        cancelled,
        inTransit,
        pending,
        lowStockCount,
        total == 0 ? 0 : Math.Round(100m * completed / total, 2),
        emergencyRows.Count,
        emergencyCompletedWithin30Min,
        orErRows.Count,
        orErRows.Count == 0 ? 0 : Math.Round(100m * orErCompleted / orErRows.Count, 2)));
});

// Operational controls for returns, wastage, cycle count, and item risk alerts.
app.MapPost("/api/inventory/returns", async ([FromBody] ProcessReturnRequest body, AppDbContext db) =>
{
    if (body.Quantity <= 0)
        return Results.BadRequest("Quantity must be positive.");
    if (!await db.Items.AnyAsync(i => i.ItemId == body.ItemId))
        return Results.BadRequest("Unknown item.");
    if (!await db.Warehouses.AnyAsync(w => w.WarehouseId == body.WarehouseId))
        return Results.BadRequest("Unknown warehouse.");

    var record = await db.InventoryRecords.FirstOrDefaultAsync(r =>
        r.ItemId == body.ItemId &&
        r.WarehouseId == body.WarehouseId &&
        r.BatchLotNumber == body.BatchLotNumber &&
        r.ExpiryDate.Date == body.ExpiryDate.Date);

    if (record is null)
    {
        var receivingSlot = await db.StoragePositions.AsNoTracking()
            .Where(p => p.WarehouseId == body.WarehouseId && p.PositionCode == "RECEIVING")
            .Select(p => new { p.StoragePositionId, p.RackCode, p.ShelfLevel, p.MapPercentX, p.MapPercentY, p.PositionCode })
            .FirstOrDefaultAsync();
        db.InventoryRecords.Add(new InventoryRecord
        {
            RecordId = Guid.NewGuid(),
            WarehouseId = body.WarehouseId,
            ItemId = body.ItemId,
            BatchLotNumber = body.BatchLotNumber.Trim(),
            ExpiryDate = body.ExpiryDate.Date,
            QuantityOnHand = body.Quantity,
            LocationBin = receivingSlot?.PositionCode ?? "RECEIVING",
            StoragePositionId = receivingSlot?.StoragePositionId,
            RackCode = receivingSlot?.RackCode ?? "RCV",
            ShelfLevel = receivingSlot?.ShelfLevel ?? 0,
            MapPercentX = receivingSlot?.MapPercentX ?? 88,
            MapPercentY = receivingSlot?.MapPercentY ?? 90
        });
    }
    else
    {
        record.QuantityOnHand += body.Quantity;
    }

    await db.SaveChangesAsync();
    return Results.Ok(new { Message = "Return processed.", body.ItemId, body.WarehouseId, body.Quantity, body.ReasonCode });
});

app.MapPost("/api/inventory/wastage", async ([FromBody] ReportWastageRequest body, AppDbContext db) =>
{
    if (body.Quantity <= 0)
        return Results.BadRequest("Quantity must be positive.");
    var record = await db.InventoryRecords.FirstOrDefaultAsync(r => r.RecordId == body.RecordId);
    if (record is null)
        return Results.NotFound();
    if (record.QuantityOnHand < body.Quantity)
        return Results.BadRequest("Insufficient quantity for adjustment.");

    record.QuantityOnHand -= body.Quantity;
    await db.SaveChangesAsync();
    return Results.Ok(new
    {
        Message = "Wastage recorded.",
        record.RecordId,
        RemainingQty = record.QuantityOnHand,
        body.ReasonCode
    });
});

app.MapPost("/api/inventory/cycle-count/reconcile", async ([FromBody] CycleCountReconcileRequest body, AppDbContext db) =>
{
    if (body.Lines is null || body.Lines.Count == 0)
        return Results.BadRequest("At least one count line is required.");

    var results = new List<CycleCountVarianceDto>();
    foreach (var line in body.Lines)
    {
        if (line.CountedQuantity < 0)
            return Results.BadRequest("CountedQuantity cannot be negative.");
        var record = await db.InventoryRecords.FirstOrDefaultAsync(r => r.RecordId == line.RecordId);
        if (record is null)
            return Results.BadRequest($"Unknown record {line.RecordId}");
        var beforeQty = record.QuantityOnHand;
        var variance = line.CountedQuantity - beforeQty;
        record.QuantityOnHand = line.CountedQuantity;
        results.Add(new CycleCountVarianceDto(line.RecordId, beforeQty, line.CountedQuantity, variance, line.ReasonCode));
    }

    await db.SaveChangesAsync();
    return Results.Ok(new CycleCountResultDto(DateTime.UtcNow, results, results.Sum(x => x.Variance)));
});

app.MapGet("/api/inventory/alerts/near-expiry", async ([FromQuery] int days, AppDbContext db) =>
{
    var thresholdDays = days <= 0 ? 60 : Math.Min(days, 365);
    var today = DateTime.UtcNow.Date;
    var cutoff = today.AddDays(thresholdDays);
    var rows = await (
        from r in db.InventoryRecords.AsNoTracking()
        join i in db.Items.AsNoTracking() on r.ItemId equals i.ItemId
        join w in db.Warehouses.AsNoTracking() on r.WarehouseId equals w.WarehouseId
        where r.QuantityOnHand > 0 && r.ExpiryDate >= today && r.ExpiryDate <= cutoff
        orderby r.ExpiryDate, i.ItemName
        select new
        {
            r.RecordId,
            i.ItemId,
            i.ItemName,
            w.WarehouseId,
            WarehouseCode = w.Code,
            r.BatchLotNumber,
            r.QuantityOnHand,
            r.ExpiryDate
        }
    ).ToListAsync();
    var mapped = rows.Select(r => new NearExpiryAlertDto(
        r.RecordId,
        r.ItemId,
        r.ItemName,
        r.WarehouseId,
        r.WarehouseCode,
        r.BatchLotNumber,
        r.QuantityOnHand,
        r.ExpiryDate,
        (r.ExpiryDate.Date - today).Days))
        .ToList();
    return Results.Ok(mapped);
});

app.MapGet("/api/users", async ([FromQuery] string? role, AppDbContext db) =>
{
    var users = db.Users.AsNoTracking().AsQueryable();
    if (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<SystemRole>(role, true, out var parsedRole))
        users = users.Where(u => u.SystemRole == parsedRole);
    var list = await users
        .OrderBy(u => u.FullName)
        .Select(u => new UserSummaryDto(u.UserId, u.FullName, u.Email, u.Department, u.SystemRole.ToString(), u.IsActive))
        .ToListAsync();
    return Results.Ok(list);
});

app.MapPost("/api/users/{id:guid}/active", async (Guid id, [FromBody] SetUserActiveRequest body, AppDbContext db) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == id);
    if (user is null)
        return Results.NotFound();
    user.IsActive = body.IsActive;
    await db.SaveChangesAsync();
    return Results.Ok(new { user.UserId, user.IsActive });
});

app.MapGet("/api/delivery-tasks", async ([FromQuery] string? status, AppDbContext db) =>
{
    var tasks = db.DeliveryTasks.AsNoTracking()
        .Include(t => t.Requisition)
        .Include(t => t.AssignedTo)
        .AsQueryable();
    if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<DeliveryStatus>(status, true, out var parsedStatus))
        tasks = tasks.Where(t => t.DeliveryStatus == parsedStatus);

    var list = await tasks
        .OrderByDescending(t => t.DispatchTime)
        .ThenByDescending(t => t.TaskId)
        .Select(t => new DeliveryQueueDto(
            t.TaskId,
            t.RequisitionId,
            t.Requisition is EmergencyRequisition,
            t.DeliveryStatus.ToString(),
            t.DispatchTime,
            t.AssignedToId,
            t.AssignedTo != null ? t.AssignedTo.FullName : null,
            t.Requisition.DeliveryLocation))
        .ToListAsync();
    return Results.Ok(list);
});

app.MapGet("/api/requisitions/{id:guid}/notifications", async (Guid id, AppDbContext db) =>
{
    var requisition = await db.Requisitions.AsNoTracking()
        .Include(r => r.PickLists)
        .Include(r => r.DeliveryTask)
        .FirstOrDefaultAsync(r => r.RequisitionId == id);
    if (requisition is null)
        return Results.NotFound();

    var feed = new List<NotificationDto>
    {
        new("request-received", requisition.RequestDate, "Request is submitted."),
        new("approval", requisition.RequestDate, requisition.Status is RequisitionStatus.Approved or RequisitionStatus.InTransit or RequisitionStatus.Completed
            ? "Request approved by inventory manager."
            : "Request pending manager approval.")
    };
    if (requisition.PickLists.Any(p => p.PickStatus == PickStatus.Packed))
    {
        var packedAt = requisition.PickLists.Max(p => p.CreationTimestamp);
        feed.Add(new NotificationDto("packed", packedAt, "Items packed and ready for delivery."));
    }
    if (requisition.DeliveryTask is not null)
    {
        if (requisition.DeliveryTask.DispatchTime.HasValue)
            feed.Add(new NotificationDto("in-transit", requisition.DeliveryTask.DispatchTime.Value, "Courier is delivering supplies."));
        if (requisition.DeliveryTask.DeliveryStatus == DeliveryStatus.Arrived)
            feed.Add(new NotificationDto("arrived", DateTime.UtcNow, "Supplies arrived and waiting for receipt confirmation."));
    }
    if (requisition.Status == RequisitionStatus.Completed)
        feed.Add(new NotificationDto("completed", DateTime.UtcNow, "Request fulfilled and closed."));

    return Results.Ok(feed.OrderBy(x => x.Timestamp));
});

// Reporting endpoints for operational review and planning meetings.
app.MapGet("/api/reports/consumption-by-department", async ([FromQuery] int days, AppDbContext db) =>
{
    var rangeDays = days <= 0 ? 30 : Math.Min(days, 365);
    var fromDate = DateTime.UtcNow.AddDays(-rangeDays);
    var rows = await (
        from r in db.Requisitions.AsNoTracking()
        join u in db.Users.AsNoTracking() on r.RequestedById equals u.UserId
        join li in db.RequisitionLineItems.AsNoTracking() on r.RequisitionId equals li.RequisitionId
        join i in db.Items.AsNoTracking() on li.ItemId equals i.ItemId
        where r.RequestDate >= fromDate && r.Status == RequisitionStatus.Completed
        group new { li, i } by new { u.Department, i.ItemId, i.ItemName } into g
        orderby g.Key.Department, g.Sum(x => x.li.FulfilledQuantity) descending
        select new DepartmentConsumptionDto(
            g.Key.Department,
            g.Key.ItemId,
            g.Key.ItemName,
            g.Sum(x => x.li.FulfilledQuantity))
    ).ToListAsync();
    return Results.Ok(rows);
});

app.MapGet("/api/reports/low-stock-risk", async (AppDbContext db) =>
{
    var profiles = await db.ItemWarehouseProfiles.AsNoTracking()
        .Include(p => p.Item)
        .Include(p => p.Warehouse)
        .ToListAsync();
    var result = new List<LowStockRiskDto>();
    foreach (var profile in profiles)
    {
        var onHand = await db.InventoryRecords.AsNoTracking()
            .Where(r => r.ItemId == profile.ItemId && r.WarehouseId == profile.WarehouseId)
            .SumAsync(r => r.QuantityOnHand);
        if (onHand >= profile.ReorderPoint)
            continue;
        var deficit = profile.ReorderPoint - onHand;
        var riskLevel = deficit >= profile.ReorderPoint * 0.5 ? "high" : "medium";
        result.Add(new LowStockRiskDto(
            profile.ItemId,
            profile.Item.ItemName,
            profile.WarehouseId,
            profile.Warehouse.Code,
            onHand,
            profile.ReorderPoint,
            deficit,
            riskLevel));
    }
    return Results.Ok(result.OrderByDescending(x => x.DeficitQty).ThenBy(x => x.ItemName));
});

app.Run();

static async Task<IResult?> ValidateRequisitionRequestAsync(
    Guid requestedById,
    string deliveryLocation,
    IReadOnlyList<CreateRequisitionLineRequest> lines,
    AppDbContext db)
{
    if (!await db.Users.AnyAsync(u => u.UserId == requestedById))
        return Results.BadRequest("RequestedById user does not exist.");
    if (string.IsNullOrWhiteSpace(deliveryLocation))
        return Results.BadRequest("DeliveryLocation is required.");
    if (lines is null || lines.Count == 0)
        return Results.BadRequest("At least one line is required.");

    foreach (var line in lines)
    {
        if (line.Quantity <= 0)
            return Results.BadRequest("Line quantity must be positive.");
        if (!await db.Items.AnyAsync(i => i.ItemId == line.ItemId))
            return Results.BadRequest($"Unknown item {line.ItemId}");
    }
    return null;
}

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
    Guid? RequestedByUserId,
    string? Priority,
    string? Justification);

internal sealed record CompleteStockTransferRequest([Required] Guid CompletedByUserId);

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

internal sealed record CreateStandardRequisitionRequest(
    [Required] Guid RequestedById,
    [Required] string DeliveryLocation,
    [Required] string TargetDeliveryWindow,
    [Required] IReadOnlyList<CreateRequisitionLineRequest> Lines);

internal sealed record CreateEmergencyRequisitionRequest(
    [Required] Guid RequestedById,
    [Required] string DeliveryLocation,
    [Required] string JustificationCode,
    [Required] IReadOnlyList<CreateRequisitionLineRequest> Lines);

internal sealed record CreateRequisitionLineRequest([Required] Guid ItemId, [Range(1, int.MaxValue)] int Quantity);

internal sealed record ApproveRequisitionRequest([Required] Guid ApprovedByUserId, string? ApprovalNote);

internal sealed record RejectRequisitionRequest([Required] Guid RejectedByUserId, string? Reason);

internal sealed record PickAndPackRequest([Required] Guid PickedByUserId);

internal sealed record CreateDeliveryTaskRequest([Required] Guid AssignedToId);

internal sealed record RequisitionItemDto(Guid ItemId, string ItemName, int RequestedQuantity, int FulfilledQuantity);

internal sealed record RequisitionSummaryDto(
    Guid RequisitionId,
    bool IsEmergency,
    string Status,
    DateTime RequestDate,
    Guid RequestedById,
    string RequestedByName,
    string DeliveryLocation,
    int RequestedTotal,
    int FulfilledTotal,
    IReadOnlyList<RequisitionItemDto> Lines);

internal sealed record PickListDto(Guid PickListId, string PickStatus, DateTime CreationTimestamp, Guid GeneratedById);

internal sealed record DeliveryTaskDto(Guid TaskId, string DeliveryStatus, DateTime? DispatchTime, Guid? AssignedToId, string? AssignedToName);

internal sealed record RequisitionDetailDto(
    Guid RequisitionId,
    bool IsEmergency,
    string Status,
    DateTime RequestDate,
    string RequestedByName,
    string DeliveryLocation,
    IReadOnlyList<PickListDto> PickLists,
    DeliveryTaskDto? DeliveryTask,
    IReadOnlyList<RequisitionItemDto> Lines);

internal sealed record PickShortageDto(Guid ItemId, int RequestedQuantity, int FulfilledQuantity);

internal sealed record RequisitionTimelineEventDto(string Stage, DateTime Timestamp, string Message);

internal sealed record SubstitutionDto(Guid ItemId, string ItemName, string? Specification, int AvailableQty);

internal sealed record ItemSubstitutionResponseDto(Guid RequestedItemId, string RequestedItemName, IReadOnlyList<SubstitutionDto> Substitutions);

internal sealed record ReplenishmentSuggestionDto(
    Guid ItemId,
    string ItemName,
    Guid WarehouseId,
    string WarehouseCode,
    int OnHand,
    int ReorderPoint,
    int SuggestedQty);

internal sealed record KpiDashboardDto(
    int RequisitionsLast30Days,
    int CompletedLast30Days,
    int CancelledLast30Days,
    int InTransitNow,
    int PendingNow,
    int LowStockProfiles,
    decimal FulfillmentRatePercent,
    int EmergencyRequestsLast30Days,
    int EmergencyCompletedWithin30Min,
    int OrErRequestsLast30Days,
    decimal OrErFulfillmentRatePercent);

internal sealed record ProcessReturnRequest(
    [Required] Guid ItemId,
    [Required] Guid WarehouseId,
    [Required] string BatchLotNumber,
    [Required] DateTime ExpiryDate,
    [Range(1, int.MaxValue)] int Quantity,
    string? ReasonCode);

internal sealed record ReportWastageRequest(
    [Required] Guid RecordId,
    [Range(1, int.MaxValue)] int Quantity,
    string? ReasonCode);

internal sealed record CycleCountReconcileRequest([Required] IReadOnlyList<CycleCountLineRequest> Lines);

internal sealed record CycleCountLineRequest([Required] Guid RecordId, [Range(0, int.MaxValue)] int CountedQuantity, string? ReasonCode);

internal sealed record CycleCountVarianceDto(Guid RecordId, int BeforeQty, int CountedQty, int Variance, string? ReasonCode);

internal sealed record CycleCountResultDto(DateTime ReconciledAt, IReadOnlyList<CycleCountVarianceDto> Variances, int NetVariance);

internal sealed record NearExpiryAlertDto(
    Guid RecordId,
    Guid ItemId,
    string ItemName,
    Guid WarehouseId,
    string WarehouseCode,
    string BatchLotNumber,
    int QuantityOnHand,
    DateTime ExpiryDate,
    int DaysToExpiry);

internal sealed record UserSummaryDto(Guid UserId, string FullName, string Email, string Department, string Role, bool IsActive);

internal sealed record SetUserActiveRequest(bool IsActive);

internal sealed record DeliveryQueueDto(
    Guid TaskId,
    Guid RequisitionId,
    bool IsEmergency,
    string DeliveryStatus,
    DateTime? DispatchTime,
    Guid? AssignedToId,
    string? AssignedToName,
    string DeliveryLocation);

internal sealed record NotificationDto(string Stage, DateTime Timestamp, string Message);

internal sealed record DepartmentConsumptionDto(string Department, Guid ItemId, string ItemName, int FulfilledQty);

internal sealed record LowStockRiskDto(
    Guid ItemId,
    string ItemName,
    Guid WarehouseId,
    string WarehouseCode,
    int OnHand,
    int ReorderPoint,
    int DeficitQty,
    string RiskLevel);

public partial class Program;
