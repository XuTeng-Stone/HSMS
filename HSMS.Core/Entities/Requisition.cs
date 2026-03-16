using HSMS.Core.Enums;

namespace HSMS.Core.Entities;

public abstract class Requisition
{
    public Guid RequisitionId { get; set; }
    public DateTime RequestDate { get; set; }
    public Guid RequestedById { get; set; }
    public RequisitionStatus Status { get; set; }
    public string DeliveryLocation { get; set; } = string.Empty;
    public User RequestedBy { get; set; } = null!;
    public ICollection<RequisitionLineItem> RequisitionLineItems { get; set; } = new List<RequisitionLineItem>();
    public ICollection<PickList> PickLists { get; set; } = new List<PickList>();
    public DeliveryTask? DeliveryTask { get; set; }
}
