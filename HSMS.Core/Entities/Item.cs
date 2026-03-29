using HSMS.Core.Enums;

namespace HSMS.Core.Entities;

/// <summary>
/// Master data for supply items. Category drives tiered commission (e.g. 1-3% standard, 3-8% HighValueImplants).
/// </summary>
public class Item
{
    public Guid ItemId { get; set; }
    /// <summary>
    /// Category determines commission tier for billing; HighValueImplants use higher rates.
    /// </summary>
    public ItemCategory Category { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? SpecificationText { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public int MinimumThreshold { get; set; }
    public ICollection<InventoryRecord> InventoryRecords { get; set; } = new List<InventoryRecord>();
    public ICollection<RequisitionLineItem> RequisitionLineItems { get; set; } = new List<RequisitionLineItem>();
}
