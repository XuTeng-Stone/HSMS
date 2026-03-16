namespace HSMS.Core.Entities;

public class LogisticsStaff : User
{
    public string? ActiveVehicleId { get; set; }
    public ICollection<DeliveryTask> DeliveryTasks { get; set; } = new List<DeliveryTask>();
}
