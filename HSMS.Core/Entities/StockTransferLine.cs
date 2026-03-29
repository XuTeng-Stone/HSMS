namespace HSMS.Core.Entities;

public class StockTransferLine
{
    public Guid StockTransferLineId { get; set; }
    public Guid StockTransferOrderId { get; set; }
    public Guid ItemId { get; set; }
    public int Quantity { get; set; }
    public StockTransferOrder StockTransferOrder { get; set; } = null!;
    public Item Item { get; set; } = null!;
}
