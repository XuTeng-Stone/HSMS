using HSMS.Core.Enums;

namespace HSMS.Core.Entities;

public class DeliveryTask
{
    public Guid TaskId { get; set; }
    public Guid RequisitionId { get; set; }
    public Guid? AssignedToId { get; set; }
    public DateTime? DispatchTime { get; set; }
    public DeliveryStatus DeliveryStatus { get; set; }
    public Requisition Requisition { get; set; } = null!;
    public LogisticsStaff? AssignedTo { get; set; }
}
