using HSMS.Core.Enums;

namespace HSMS.Core.Entities;

public class StockTransferOrder
{
    public Guid StockTransferOrderId { get; set; }
    public Guid SourceWarehouseId { get; set; }
    public Guid DestinationWarehouseId { get; set; }
    public DateTime RequestedAt { get; set; }
    public StockTransferOrderStatus Status { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public Warehouse SourceWarehouse { get; set; } = null!;
    public Warehouse DestinationWarehouse { get; set; } = null!;
    public User? RequestedBy { get; set; }
    public User? CompletedBy { get; set; }
    public ICollection<StockTransferLine> Lines { get; set; } = new List<StockTransferLine>();
}
