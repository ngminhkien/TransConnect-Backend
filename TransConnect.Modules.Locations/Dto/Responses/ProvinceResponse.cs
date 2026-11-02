using TransConnect.Modules.Locations.Entities;

namespace TransConnect.Modules.Locations.Dto.Responses;

public sealed record ProvinceResponse
{
    public Guid DatasetId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";

    public static ProvinceResponse From(Province value) => new()
    {
        DatasetId = value.DatasetId,
        Code = value.Code,
        Name = value.Name,
    };
}
