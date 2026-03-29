using HSMS.Core.Entities;
using HSMS.Core.Enums;
using HSMS.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace HSMS.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Warehouses.AnyAsync())
            return;

        var centralId = Guid.Parse("11111111-1111-1111-1111-111111111101");
        var satelliteId = Guid.Parse("11111111-1111-1111-1111-111111111102");

        db.Warehouses.AddRange(
            new Warehouse
            {
                WarehouseId = centralId,
                Code = "WH_CENTRAL",
                DisplayName = "Remote Central Warehouse",
                IsCentralHub = true
            },
            new Warehouse
            {
                WarehouseId = satelliteId,
                Code = "WH_SATELLITE",
                DisplayName = "Basement Satellite Store",
                IsCentralHub = false
            });

        var itemA = new Item
        {
            ItemId = Guid.Parse("22222222-2222-2222-2222-222222222201"),
            Category = ItemCategory.Pharmaceuticals,
            ItemName = "Amoxicillin Capsules",
            SpecificationText = "500 mg; oral; bottle 100 caps",
            UnitOfMeasure = "bottle",
            MinimumThreshold = 20
        };
        var itemB = new Item
        {
            ItemId = Guid.Parse("22222222-2222-2222-2222-222222222202"),
            Category = ItemCategory.Pharmaceuticals,
            ItemName = "Normal Saline IV",
            SpecificationText = "0.9%; 500 mL bag",
            UnitOfMeasure = "bag",
            MinimumThreshold = 30
        };
        var itemC = new Item
        {
            ItemId = Guid.Parse("22222222-2222-2222-2222-222222222203"),
            Category = ItemCategory.Surgical,
            ItemName = "Sterile Gauze Pads",
            SpecificationText = "10x10 cm; 4-ply; pack 50",
            UnitOfMeasure = "pack",
            MinimumThreshold = 15
        };
        db.Items.AddRange(itemA, itemB, itemC);

        db.ItemWarehouseProfiles.AddRange(
            new ItemWarehouseProfile
            {
                ItemWarehouseProfileId = Guid.NewGuid(),
                ItemId = itemA.ItemId,
                WarehouseId = satelliteId,
                SafetyStockCeiling = 80,
                ReorderPoint = 25
            },
            new ItemWarehouseProfile
            {
                ItemWarehouseProfileId = Guid.NewGuid(),
                ItemId = itemA.ItemId,
                WarehouseId = centralId,
                SafetyStockCeiling = 2000,
                ReorderPoint = 400
            },
            new ItemWarehouseProfile
            {
                ItemWarehouseProfileId = Guid.NewGuid(),
                ItemId = itemB.ItemId,
                WarehouseId = satelliteId,
                SafetyStockCeiling = 120,
                ReorderPoint = 40
            },
            new ItemWarehouseProfile
            {
                ItemWarehouseProfileId = Guid.NewGuid(),
                ItemId = itemB.ItemId,
                WarehouseId = centralId,
                SafetyStockCeiling = 1500,
                ReorderPoint = 300
            },
            new ItemWarehouseProfile
            {
                ItemWarehouseProfileId = Guid.NewGuid(),
                ItemId = itemC.ItemId,
                WarehouseId = satelliteId,
                SafetyStockCeiling = 60,
                ReorderPoint = 20
            },
            new ItemWarehouseProfile
            {
                ItemWarehouseProfileId = Guid.NewGuid(),
                ItemId = itemC.ItemId,
                WarehouseId = centralId,
                SafetyStockCeiling = 800,
                ReorderPoint = 150
            });

        var exp1 = new DateTime(2027, 6, 30);
        var exp2 = new DateTime(2027, 9, 15);

        db.InventoryRecords.AddRange(
            new InventoryRecord
            {
                RecordId = Guid.NewGuid(),
                WarehouseId = satelliteId,
                ItemId = itemA.ItemId,
                BatchLotNumber = "AMX-2401",
                ExpiryDate = exp1,
                QuantityOnHand = 18,
                LocationBin = "SAT-B12-03"
            },
            new InventoryRecord
            {
                RecordId = Guid.NewGuid(),
                WarehouseId = centralId,
                ItemId = itemA.ItemId,
                BatchLotNumber = "AMX-2401",
                ExpiryDate = exp1,
                QuantityOnHand = 600,
                LocationBin = "CEN-A01-07"
            },
            new InventoryRecord
            {
                RecordId = Guid.NewGuid(),
                WarehouseId = centralId,
                ItemId = itemA.ItemId,
                BatchLotNumber = "AMX-2402",
                ExpiryDate = exp2,
                QuantityOnHand = 400,
                LocationBin = "CEN-A01-08"
            },
            new InventoryRecord
            {
                RecordId = Guid.NewGuid(),
                WarehouseId = satelliteId,
                ItemId = itemB.ItemId,
                BatchLotNumber = "NS500-778",
                ExpiryDate = exp2,
                QuantityOnHand = 55,
                LocationBin = "SAT-C04-01"
            },
            new InventoryRecord
            {
                RecordId = Guid.NewGuid(),
                WarehouseId = centralId,
                ItemId = itemB.ItemId,
                BatchLotNumber = "NS500-778",
                ExpiryDate = exp2,
                QuantityOnHand = 900,
                LocationBin = "CEN-B02-01"
            },
            new InventoryRecord
            {
                RecordId = Guid.NewGuid(),
                WarehouseId = satelliteId,
                ItemId = itemC.ItemId,
                BatchLotNumber = "GZ-55-990",
                ExpiryDate = exp1,
                QuantityOnHand = 35,
                LocationBin = "SAT-D01-02"
            },
            new InventoryRecord
            {
                RecordId = Guid.NewGuid(),
                WarehouseId = centralId,
                ItemId = itemC.ItemId,
                BatchLotNumber = "GZ-55-990",
                ExpiryDate = exp1,
                QuantityOnHand = 420,
                LocationBin = "CEN-C03-04"
            });

        await db.SaveChangesAsync();
    }
}
