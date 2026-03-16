using HSMS.Core.Enums;

namespace HSMS.Core.Entities;

public abstract class User
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public SystemRole SystemRole { get; set; }
    public bool IsActive { get; set; }
    public ICollection<Requisition> Requisitions { get; set; } = new List<Requisition>();
}
