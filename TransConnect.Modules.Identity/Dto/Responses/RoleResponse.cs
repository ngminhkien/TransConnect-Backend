namespace TransConnect.Modules.Identity.Dto.Responses;

public record RoleResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
