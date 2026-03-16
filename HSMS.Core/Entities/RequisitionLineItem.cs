namespace HSMS.Core.Entities;

public class RequisitionLineItem
{
    public Guid LineItemId { get; set; }
    public Guid RequisitionId { get; set; }
    public Guid ItemId { get; set; }
    public int RequestedQuantity { get; set; }
    public int FulfilledQuantity { get; set; }
    public Requisition Requisition { get; set; } = null!;
    public Item Item { get; set; } = null!;
}
