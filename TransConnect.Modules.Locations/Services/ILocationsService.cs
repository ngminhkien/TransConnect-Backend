using TransConnect.Common.Response;
using TransConnect.Modules.Locations.Dto.Requests;
using TransConnect.Modules.Locations.Dto.Responses;

namespace TransConnect.Modules.Locations.Services;

public interface ILocationsService
{
    Task<AdministrativeDatasetResponse> ActiveDatasetAsync(CancellationToken ct);
    Task<IReadOnlyList<ProvinceResponse>> ProvincesAsync(CancellationToken ct);
    Task<IReadOnlyList<WardResponse>> WardsAsync(string provinceCode, CancellationToken ct);
    Task<AdministrativeDatasetResponse> ImportAsync(ImportDatasetRequest request, CancellationToken ct);
    Task<IReadOnlyList<CustomerAddressResponse>> AddressesAsync(CancellationToken ct);
    Task<CustomerAddressResponse> AddressAsync(Guid id, CancellationToken ct);
    Task<CustomerAddressResponse> SaveAddressAsync(Guid? id, SaveAddressRequest request, CancellationToken ct);
    Task ArchiveAddressAsync(Guid id, int version, CancellationToken ct);
    Task<PagedResult<SharedLocationResponse>> SharedAsync(string? search, string? provinceCode, string? type, int pageNumber, int pageSize, CancellationToken ct);
    Task<PagedResult<SharedLocationResponse>> AdminSharedAsync(string? search, int pageNumber, int pageSize, CancellationToken ct);
    Task<SharedLocationResponse> SharedDetailAsync(Guid id, CancellationToken ct);
    Task<SharedLocationResponse> SaveSharedAsync(Guid? id, SaveSharedLocationRequest request, CancellationToken ct);
    Task ArchiveSharedAsync(Guid id, ArchiveLocationRequest request, CancellationToken ct);
    Task<IReadOnlyList<SharedLocationVersionResponse>> HistoryAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<LocationChangeRequestResponse>> RequestsAsync(Guid? organizationId, string? status, bool admin, CancellationToken ct);
    Task<LocationChangeRequestResponse> SubmitAsync(SubmitLocationRequest request, CancellationToken ct);
    Task<LocationChangeRequestResponse> ReviewAsync(Guid id, ReviewLocationRequest request, CancellationToken ct);
}
