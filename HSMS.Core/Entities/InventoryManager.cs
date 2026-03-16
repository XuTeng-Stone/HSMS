namespace HSMS.Core.Entities;

public class InventoryManager : User
{
    public string AssignedWarehouseZone { get; set; } = string.Empty;
    public ICollection<PickList> PickLists { get; set; } = new List<PickList>();
}
