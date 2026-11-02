using TransConnect.Modules.Locations.Entities;

namespace TransConnect.Modules.Locations.Dto.Responses;

public sealed record WardResponse
{
    public Guid DatasetId { get; init; }
    public string Code { get; init; } = "";
    public string ProvinceCode { get; init; } = "";
    public string Name { get; init; } = "";

    public static WardResponse From(Ward value) => new()
    {
        DatasetId = value.DatasetId,
        Code = value.Code,
        ProvinceCode = value.ProvinceCode,
        Name = value.Name,
    };
}
