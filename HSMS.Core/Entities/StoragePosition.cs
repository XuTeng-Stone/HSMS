namespace HSMS.Core.Entities;

public class StoragePosition
{
    public Guid StoragePositionId { get; set; }
    public Guid WarehouseId { get; set; }
    public string PositionCode { get; set; } = string.Empty;
    public string RackCode { get; set; } = string.Empty;
    public int ShelfLevel { get; set; }
    public int MapPercentX { get; set; }
    public int MapPercentY { get; set; }
    public string? AisleLabel { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public ICollection<InventoryRecord> InventoryRecords { get; set; } = new List<InventoryRecord>();
}
