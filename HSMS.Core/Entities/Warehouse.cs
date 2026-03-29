namespace HSMS.Core.Entities;

public class Warehouse
{
    public Guid WarehouseId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsCentralHub { get; set; }
    public ICollection<ItemWarehouseProfile> ItemWarehouseProfiles { get; set; } = new List<ItemWarehouseProfile>();
    public ICollection<InventoryRecord> InventoryRecords { get; set; } = new List<InventoryRecord>();
    public ICollection<StockTransferOrder> OutboundTransfers { get; set; } = new List<StockTransferOrder>();
    public ICollection<StockTransferOrder> InboundTransfers { get; set; } = new List<StockTransferOrder>();
}
