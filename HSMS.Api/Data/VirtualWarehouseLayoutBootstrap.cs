using HSMS.Core.Data;
using HSMS.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace HSMS.Api.Data;

public static class VirtualWarehouseLayoutBootstrap
{
    public static async Task EnsureAsync(AppDbContext db)
    {
        if (await db.WarehouseZones.AnyAsync())
            return;
        if (!await db.Warehouses.AnyAsync())
            return;

        var whList = await db.Warehouses.AsNoTracking().ToListAsync();
        var satellite = whList.FirstOrDefault(w => w.Code == "WH_SATELLITE") ?? whList.FirstOrDefault(w => !w.IsCentralHub);
        var central = whList.FirstOrDefault(w => w.Code == "WH_CENTRAL") ?? whList.FirstOrDefault(w => w.IsCentralHub);
        if (satellite is null || central is null)
            return;

        AddZones(db, satellite.WarehouseId, central.WarehouseId);
        await db.SaveChangesAsync();

        await EnsureReceivingPositions(db, satellite.WarehouseId, central.WarehouseId);
        await EnsurePositionsFromInventory(db);
        await db.SaveChangesAsync();
        await LinkInventoryToPositions(db);
        await db.SaveChangesAsync();
    }

    private static void AddZones(AppDbContext db, Guid satelliteId, Guid centralId)
    {
        db.WarehouseZones.AddRange(
            new WarehouseZone { WarehouseZoneId = Guid.NewGuid(), WarehouseId = satelliteId, Label = "A", SortOrder = 1, RectX = 8, RectY = 22, RectW = 18, RectH = 52 },
            new WarehouseZone { WarehouseZoneId = Guid.NewGuid(), WarehouseId = satelliteId, Label = "B", SortOrder = 2, RectX = 32, RectY = 18, RectW = 22, RectH = 58 },
            new WarehouseZone { WarehouseZoneId = Guid.NewGuid(), WarehouseId = satelliteId, Label = "C", SortOrder = 3, RectX = 58, RectY = 28, RectW = 20, RectH = 46 },
            new WarehouseZone { WarehouseZoneId = Guid.NewGuid(), WarehouseId = satelliteId, Label = "D", SortOrder = 4, RectX = 82, RectY = 20, RectW = 14, RectH = 66 },
            new WarehouseZone { WarehouseZoneId = Guid.NewGuid(), WarehouseId = centralId, Label = "A", SortOrder = 1, RectX = 5, RectY = 16, RectW = 13, RectH = 68 },
            new WarehouseZone { WarehouseZoneId = Guid.NewGuid(), WarehouseId = centralId, Label = "B", SortOrder = 2, RectX = 21, RectY = 16, RectW = 13, RectH = 68 },
            new WarehouseZone { WarehouseZoneId = Guid.NewGuid(), WarehouseId = centralId, Label = "C", SortOrder = 3, RectX = 37, RectY = 16, RectW = 13, RectH = 68 },
            new WarehouseZone { WarehouseZoneId = Guid.NewGuid(), WarehouseId = centralId, Label = "D", SortOrder = 4, RectX = 53, RectY = 16, RectW = 13, RectH = 68 },
            new WarehouseZone { WarehouseZoneId = Guid.NewGuid(), WarehouseId = centralId, Label = "E", SortOrder = 5, RectX = 69, RectY = 16, RectW = 13, RectH = 68 },
            new WarehouseZone { WarehouseZoneId = Guid.NewGuid(), WarehouseId = centralId, Label = "V", SortOrder = 6, RectX = 88, RectY = 14, RectW = 8, RectH = 72 });
    }

    private static async Task EnsureReceivingPositions(AppDbContext db, Guid satelliteId, Guid centralId)
    {
        foreach (var wid in new[] { satelliteId, centralId })
        {
            if (await db.StoragePositions.AnyAsync(p => p.WarehouseId == wid && p.PositionCode == "RECEIVING"))
                continue;
            db.StoragePositions.Add(new StoragePosition
            {
                StoragePositionId = Guid.NewGuid(),
                WarehouseId = wid,
                PositionCode = "RECEIVING",
                RackCode = "RCV",
                ShelfLevel = 0,
                MapPercentX = 88,
                MapPercentY = 90,
                AisleLabel = "Dock"
            });
        }
    }

    private static async Task EnsurePositionsFromInventory(AppDbContext db)
    {
        var records = await db.InventoryRecords.ToListAsync();
        foreach (var grp in records.GroupBy(r => new { r.WarehouseId, r.LocationBin }))
        {
            var key = grp.Key;
            if (string.IsNullOrWhiteSpace(key.LocationBin))
                continue;
            if (await db.StoragePositions.AnyAsync(p => p.WarehouseId == key.WarehouseId && p.PositionCode == key.LocationBin))
                continue;
            var r0 = grp.First();
            var mx = r0.MapPercentX ?? StableHash(key.LocationBin + key.WarehouseId.ToString("N"), 12, 76);
            var my = r0.MapPercentY ?? StableHash(key.LocationBin + key.WarehouseId.ToString("N") + "|y", 12, 56);
            db.StoragePositions.Add(new StoragePosition
            {
                StoragePositionId = Guid.NewGuid(),
                WarehouseId = key.WarehouseId,
                PositionCode = key.LocationBin,
                RackCode = string.IsNullOrWhiteSpace(r0.RackCode) ? "R" : r0.RackCode,
                ShelfLevel = r0.ShelfLevel,
                MapPercentX = mx,
                MapPercentY = my,
                AisleLabel = null
            });
        }
    }

    private static int StableHash(string s, int min, int span)
    {
        unchecked
        {
            var h = 2166136261u;
            foreach (var c in s)
            {
                h ^= c;
                h *= 16777619u;
            }
            return min + (int)(h % (uint)span);
        }
    }

    private static async Task LinkInventoryToPositions(AppDbContext db)
    {
        var positions = await db.StoragePositions.AsNoTracking()
            .Select(p => new { p.StoragePositionId, p.WarehouseId, p.PositionCode })
            .ToListAsync();
        var lookup = positions.ToDictionary(p => (p.WarehouseId, p.PositionCode), p => p.StoragePositionId);
        var records = await db.InventoryRecords.Where(r => r.StoragePositionId == null).ToListAsync();
        foreach (var r in records)
        {
            if (lookup.TryGetValue((r.WarehouseId, r.LocationBin), out var sid))
                r.StoragePositionId = sid;
        }
    }
}
