using TransConnect.Base.Entities;

namespace TransConnect.Modules.Identity.Entities;

public class Permission : BaseEntity
{
    // Code dạng "resource.action", vd "roles.manage", "booking.create".
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
