namespace HSMS.Core.Entities;

public class WarehouseZone
{
    public Guid WarehouseZoneId { get; set; }
    public Guid WarehouseId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int RectX { get; set; }
    public int RectY { get; set; }
    public int RectW { get; set; }
    public int RectH { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
}
