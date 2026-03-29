namespace HSMS.Core.Entities;

public class ItemWarehouseProfile
{
    public Guid ItemWarehouseProfileId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public int SafetyStockCeiling { get; set; }
    public int ReorderPoint { get; set; }
    public Item Item { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
}
