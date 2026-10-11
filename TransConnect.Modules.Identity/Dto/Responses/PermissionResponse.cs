namespace TransConnect.Modules.Identity.Dto.Responses;

public record PermissionResponse
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string? Description { get; init; }
}
