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
            new Warehouse { WarehouseId = centralId, Code = "WH_CENTRAL", DisplayName = "Remote Central Warehouse", IsCentralHub = true },
            new Warehouse { WarehouseId = satelliteId, Code = "WH_SATELLITE", DisplayName = "Basement Satellite Store", IsCentralHub = false });

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

        var pos = new Dictionary<string, Guid>();

        void AddPos(Guid wh, string code, string rack, int shelf, int mx, int my, string? aisle = null)
        {
            var id = Guid.NewGuid();
            pos[$"{wh:N}|{code}"] = id;
            db.StoragePositions.Add(new StoragePosition
            {
                StoragePositionId = id,
                WarehouseId = wh,
                PositionCode = code,
                RackCode = rack,
                ShelfLevel = shelf,
                MapPercentX = mx,
                MapPercentY = my,
                AisleLabel = aisle
            });
        }

        AddPos(satelliteId, "RECEIVING", "RCV", 0, 88, 90, "Dock");
        AddPos(satelliteId, "SAT-B12-03", "B12", 3, 72, 38, "B");
        AddPos(satelliteId, "SAT-C04-01", "C04", 2, 45, 62, "C");
        AddPos(satelliteId, "SAT-D01-02", "D01", 2, 28, 72, "D");
        AddPos(satelliteId, "SAT-A02-11", "A02", 4, 18, 48, "A");
        AddPos(satelliteId, "SAT-A02-12", "A02", 5, 24, 48, "A");
        AddPos(satelliteId, "SAT-B08-04", "B08", 2, 58, 35, "B");
        AddPos(satelliteId, "SAT-SEC-01", "S01", 1, 82, 22, "SEC");
        AddPos(satelliteId, "SAT-E03-06", "E03", 3, 38, 58, "E");
        AddPos(satelliteId, "SAT-F01-03", "F01", 2, 62, 78, "F");

        AddPos(centralId, "RECEIVING", "RCV", 0, 88, 90, "Dock");
        AddPos(centralId, "CEN-A01-07", "A01", 4, 22, 28, "A");
        AddPos(centralId, "CEN-A01-08", "A01", 5, 28, 28, "A");
        AddPos(centralId, "CEN-B02-01", "B02", 3, 55, 45, "B");
        AddPos(centralId, "CEN-B02-02", "B02", 4, 62, 45, "B");
        AddPos(centralId, "CEN-C03-04", "C03", 2, 78, 32, "C");
        AddPos(centralId, "CEN-D04-01", "D04", 6, 35, 68, "D");
        AddPos(centralId, "CEN-D04-02", "D04", 7, 42, 68, "D");
        AddPos(centralId, "CEN-A05-03", "A05", 3, 18, 52, "A");
        AddPos(centralId, "CEN-VAULT-2", "V02", 1, 88, 18, "V");
        AddPos(centralId, "CEN-E01-09", "E01", 4, 48, 72, "E");
        AddPos(centralId, "CEN-F05-01", "F05", 5, 72, 55, "F");
        AddPos(centralId, "CEN-F05-02", "F05", 6, 78, 55, "F");

        Guid P(Guid wh, string bin) => pos[$"{wh:N}|{bin}"];

        var itemA = NewItem("22222222-2222-2222-2222-222222222201", ItemCategory.Pharmaceuticals, "Amoxicillin Capsules", "500 mg; oral; bottle 100 caps", "bottle", 20);
        var itemB = NewItem("22222222-2222-2222-2222-222222222202", ItemCategory.Pharmaceuticals, "Normal Saline IV", "0.9%; 500 mL bag", "bag", 30);
        var itemC = NewItem("22222222-2222-2222-2222-222222222203", ItemCategory.Surgical, "Sterile Gauze Pads", "10x10 cm; 4-ply; pack 50", "pack", 15);
        var itemD = NewItem("22222222-2222-2222-2222-222222222204", ItemCategory.PPE, "Nitrile Exam Gloves", "medium; powder-free; box 100", "box", 40);
        var itemE = NewItem("22222222-2222-2222-2222-222222222205", ItemCategory.Pharmaceuticals, "Paracetamol Tablets", "500 mg; blister 20", "blister", 50);
        var itemF = NewItem("22222222-2222-2222-2222-222222222206", ItemCategory.HighValueImplants, "Titanium Plate OR-TP", "radius 3.5 mm; sterile single", "each", 5);
        var itemG = NewItem("22222222-2222-2222-2222-222222222207", ItemCategory.PPE, "Surgical Face Mask", "ASTM Level 3; box 50", "box", 35);
        var itemH = NewItem("22222222-2222-2222-2222-222222222208", ItemCategory.Surgical, "Disposable Syringe", "10 mL Luer lock; pack 100", "pack", 25);
        db.Items.AddRange(itemA, itemB, itemC, itemD, itemE, itemF, itemG, itemH);

        var items = new[] { itemA, itemB, itemC, itemD, itemE, itemF, itemG, itemH };
        foreach (var it in items)
        {
            db.ItemWarehouseProfiles.Add(new ItemWarehouseProfile
            {
                ItemWarehouseProfileId = Guid.NewGuid(),
                ItemId = it.ItemId,
                WarehouseId = satelliteId,
                SafetyStockCeiling = it.ItemId == itemF.ItemId ? 12 : it.Category == ItemCategory.PPE ? 200 : it.Category == ItemCategory.Pharmaceuticals ? 150 : 80,
                ReorderPoint = 20
            });
            db.ItemWarehouseProfiles.Add(new ItemWarehouseProfile
            {
                ItemWarehouseProfileId = Guid.NewGuid(),
                ItemId = it.ItemId,
                WarehouseId = centralId,
                SafetyStockCeiling = it.ItemId == itemF.ItemId ? 120 : it.Category == ItemCategory.PPE ? 4000 : it.Category == ItemCategory.Pharmaceuticals ? 3000 : 1200,
                ReorderPoint = 200
            });
        }

        var exp1 = new DateTime(2027, 6, 30);
        var exp2 = new DateTime(2027, 9, 15);
        var exp3 = new DateTime(2027, 12, 1);

        db.InventoryRecords.AddRange(
            Inv(satelliteId, itemA.ItemId, "AMX-2401", exp1, 18, "SAT-B12-03", P(satelliteId, "SAT-B12-03"), "B12", 3, 72, 38),
            Inv(centralId, itemA.ItemId, "AMX-2401", exp1, 600, "CEN-A01-07", P(centralId, "CEN-A01-07"), "A01", 4, 22, 28),
            Inv(centralId, itemA.ItemId, "AMX-2402", exp2, 400, "CEN-A01-08", P(centralId, "CEN-A01-08"), "A01", 5, 28, 28),
            Inv(satelliteId, itemB.ItemId, "NS500-778", exp2, 55, "SAT-C04-01", P(satelliteId, "SAT-C04-01"), "C04", 2, 45, 62),
            Inv(centralId, itemB.ItemId, "NS500-778", exp2, 900, "CEN-B02-01", P(centralId, "CEN-B02-01"), "B02", 3, 55, 45),
            Inv(centralId, itemB.ItemId, "NS500-881", exp3, 620, "CEN-B02-02", P(centralId, "CEN-B02-02"), "B02", 4, 62, 45),
            Inv(satelliteId, itemC.ItemId, "GZ-55-990", exp1, 35, "SAT-D01-02", P(satelliteId, "SAT-D01-02"), "D01", 2, 28, 72),
            Inv(centralId, itemC.ItemId, "GZ-55-990", exp1, 420, "CEN-C03-04", P(centralId, "CEN-C03-04"), "C03", 2, 78, 32),
            Inv(satelliteId, itemD.ItemId, "GLV-9921", exp2, 140, "SAT-A02-11", P(satelliteId, "SAT-A02-11"), "A02", 4, 18, 48),
            Inv(satelliteId, itemD.ItemId, "GLV-9922", exp3, 90, "SAT-A02-12", P(satelliteId, "SAT-A02-12"), "A02", 5, 24, 48),
            Inv(centralId, itemD.ItemId, "GLV-9921", exp2, 2100, "CEN-D04-01", P(centralId, "CEN-D04-01"), "D04", 6, 35, 68),
            Inv(centralId, itemD.ItemId, "GLV-9922", exp3, 1800, "CEN-D04-02", P(centralId, "CEN-D04-02"), "D04", 7, 42, 68),
            Inv(satelliteId, itemE.ItemId, "PCM-110", exp1, 80, "SAT-B08-04", P(satelliteId, "SAT-B08-04"), "B08", 2, 58, 35),
            Inv(centralId, itemE.ItemId, "PCM-110", exp1, 1400, "CEN-A05-03", P(centralId, "CEN-A05-03"), "A05", 3, 18, 52),
            Inv(satelliteId, itemF.ItemId, "TIP-OR01", exp2, 4, "SAT-SEC-01", P(satelliteId, "SAT-SEC-01"), "S01", 1, 82, 22),
            Inv(centralId, itemF.ItemId, "TIP-OR01", exp2, 28, "CEN-VAULT-2", P(centralId, "CEN-VAULT-2"), "V02", 1, 88, 18),
            Inv(satelliteId, itemG.ItemId, "MSK-L3-440", exp3, 95, "SAT-E03-06", P(satelliteId, "SAT-E03-06"), "E03", 3, 38, 58),
            Inv(centralId, itemG.ItemId, "MSK-L3-440", exp3, 1600, "CEN-E01-09", P(centralId, "CEN-E01-09"), "E01", 4, 48, 72),
            Inv(satelliteId, itemH.ItemId, "SYR-10-771", exp1, 42, "SAT-F01-03", P(satelliteId, "SAT-F01-03"), "F01", 2, 62, 78),
            Inv(centralId, itemH.ItemId, "SYR-10-771", exp1, 880, "CEN-F05-01", P(centralId, "CEN-F05-01"), "F05", 5, 72, 55),
            Inv(centralId, itemH.ItemId, "SYR-10-772", exp2, 540, "CEN-F05-02", P(centralId, "CEN-F05-02"), "F05", 6, 78, 55));

        db.Users.AddRange(
            new MedicalStaff
            {
                UserId = Guid.Parse("33333333-3333-3333-3333-333333333301"),
                FullName = "Dr. Emily Carter",
                Email = "emily.carter@hsms.local",
                Department = "ER",
                ContactNumber = "1001",
                SystemRole = SystemRole.MedicalStaff,
                IsActive = true,
                LicenseNumber = "MD-ER-1001"
            },
            new MedicalStaff
            {
                UserId = Guid.Parse("33333333-3333-3333-3333-333333333302"),
                FullName = "Nurse Liam Brooks",
                Email = "liam.brooks@hsms.local",
                Department = "OR",
                ContactNumber = "1002",
                SystemRole = SystemRole.MedicalStaff,
                IsActive = true,
                LicenseNumber = "RN-OR-1002"
            },
            new InventoryManager
            {
                UserId = Guid.Parse("33333333-3333-3333-3333-333333333303"),
                FullName = "Ava Thompson",
                Email = "ava.thompson@hsms.local",
                Department = "Supply Chain",
                ContactNumber = "2001",
                SystemRole = SystemRole.InventoryManager,
                IsActive = true,
                AssignedWarehouseZone = "A"
            },
            new LogisticsStaff
            {
                UserId = Guid.Parse("33333333-3333-3333-3333-333333333304"),
                FullName = "Noah Patel",
                Email = "noah.patel@hsms.local",
                Department = "Logistics",
                ContactNumber = "3001",
                SystemRole = SystemRole.LogisticsStaff,
                IsActive = true,
                ActiveVehicleId = "VAN-01"
            });

        await db.SaveChangesAsync();
    }

    private static Item NewItem(string id, ItemCategory cat, string name, string spec, string uom, int min)
    {
        return new Item
        {
            ItemId = Guid.Parse(id),
            Category = cat,
            ItemName = name,
            SpecificationText = spec,
            UnitOfMeasure = uom,
            MinimumThreshold = min
        };
    }

    private static InventoryRecord Inv(Guid warehouseId, Guid itemId, string lot, DateTime exp, int qty, string bin, Guid storagePositionId, string rack, int shelf, int mx, int my)
    {
        return new InventoryRecord
        {
            RecordId = Guid.NewGuid(),
            WarehouseId = warehouseId,
            ItemId = itemId,
            BatchLotNumber = lot,
            ExpiryDate = exp,
            QuantityOnHand = qty,
            LocationBin = bin,
            StoragePositionId = storagePositionId,
            RackCode = rack,
            ShelfLevel = shelf,
            MapPercentX = mx,
            MapPercentY = my
        };
    }
}
