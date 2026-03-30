namespace HSMS.Core.Entities;

/// <summary>
/// Stock record per batch/lot. ExpiryDate is used for FEFO/FIFO picking and cold-chain compliance.
/// </summary>
public class InventoryRecord
{
    public Guid RecordId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid ItemId { get; set; }
    public string BatchLotNumber { get; set; } = string.Empty;
    /// <summary>
    /// Critical for FEFO/FIFO and expiry compliance.
    /// </summary>
    public DateTime ExpiryDate { get; set; }
    public int QuantityOnHand { get; set; }
    public string LocationBin { get; set; } = string.Empty;
    public string RackCode { get; set; } = string.Empty;
    public int ShelfLevel { get; set; }
    public int? MapPercentX { get; set; }
    public int? MapPercentY { get; set; }
    public Guid? StoragePositionId { get; set; }
    public StoragePosition? StoragePosition { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public Item Item { get; set; } = null!;
}
