using TransConnect.Base.Entities;

namespace TransConnect.Modules.Compliance.Entities;

public sealed class DemoProfileDocument : BaseEntity
{
    public string SubjectType { get; set; } = "";
    public Guid SubjectId { get; set; }
    public string DocumentType { get; set; } = "";
    public Guid? ScopeOrganizationId { get; set; }
    public int LastVersionNumber { get; set; }
}
