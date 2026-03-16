using HSMS.Core.Enums;

namespace HSMS.Core.Entities;

public class PickList
{
    public Guid PickListId { get; set; }
    public Guid RequisitionId { get; set; }
    public Guid GeneratedById { get; set; }
    public DateTime CreationTimestamp { get; set; }
    public PickStatus PickStatus { get; set; }
    public Requisition Requisition { get; set; } = null!;
    public InventoryManager GeneratedBy { get; set; } = null!;
}
