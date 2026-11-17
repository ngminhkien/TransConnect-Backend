namespace TransConnect.Modules.Transport.Entities;

// Lớp 1: danh mục cố định do hệ thống quản lý (seed qua migration), không có API sửa.
public sealed class ServiceType
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string AllowedModes { get; set; } = "";
    public bool IsOperational { get; set; }
    public int SortOrder { get; set; }
}
