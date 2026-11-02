using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Auth;
using TransConnect.Common.Response;
using TransConnect.Common.Transport;
using TransConnect.Modules.Locations.Dto.Requests;
using TransConnect.Modules.Locations.Dto.Responses;
using TransConnect.Modules.Locations.Entities;
using TransConnect.Modules.Locations.Persistence;

namespace TransConnect.Modules.Locations.Services;

public sealed class LocationsService(LocationsDbContext db, ICurrentUser current, IAccountDirectory accounts,
    ILocationOrganizationAccess organizations) : ILocationsService
{
    private static void Check(bool valid, string field, string message)
    {
        if (!valid) throw new ValidationAppException(new Dictionary<string, string[]> { [field] = [message] });
    }

    private static string Required(string? value, int max, string field)
    {
        Check(!string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max, field, $"Trường bắt buộc, tối đa {max} ký tự.");
        return value!.Trim();
    }

    private static void Optional(string? value, int max, string field) => Check(value is null || value.Length <= max, field, $"Tối đa {max} ký tự.");

    private async Task<Guid> ActorAsync(string? group, CancellationToken ct)
    {
        var id = current.UserId ?? throw new UnauthorizedAppException("Vui lòng đăng nhập.");
        var account = await accounts.FindActiveAsync(id, ct) ?? throw new UnauthorizedAppException("Tài khoản không hoạt động.");
        if (group is not null && account.Group != group) throw new ForbiddenAppException("Tài khoản không có quyền thực hiện thao tác này.");
        return id;
    }

    private async Task<Guid> AdminAsync(CancellationToken ct)
    {
        var id = await ActorAsync(AccountGroups.InternalAdmin, ct);
        if (!current.HasPermission("locations.manage")) throw new ForbiddenAppException("Bạn chưa có quyền quản lý địa điểm.");
        return id;
    }

    private static void Version(int expected, int actual)
    {
        if (expected != actual) throw new ConflictException("Dữ liệu đã thay đổi. Vui lòng tải lại và thử lại.");
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("Dữ liệu đã được người khác cập nhật. Vui lòng tải lại."); }
        catch (DbUpdateException) { throw new ConflictException("Dữ liệu xung đột với bản hiện có. Vui lòng kiểm tra và tải lại."); }
    }

    public async Task<AdministrativeDatasetResponse> ActiveDatasetAsync(CancellationToken ct)
    {
        await ActorAsync(null, ct);
        return AdministrativeDatasetResponse.From(await db.Datasets.AsNoTracking().SingleOrDefaultAsync(x => x.IsActive, ct)
            ?? throw new NotFoundException("Chưa có danh mục địa giới đang hoạt động."));
    }

    public async Task<IReadOnlyList<ProvinceResponse>> ProvincesAsync(CancellationToken ct)
    {
        await ActorAsync(null, ct);
        return (await db.Provinces.AsNoTracking().Where(x => db.Datasets.Any(d => d.Id == x.DatasetId && d.IsActive)).OrderBy(x => x.Code).ToListAsync(ct)).Select(ProvinceResponse.From).ToList();
    }

    public async Task<IReadOnlyList<WardResponse>> WardsAsync(string provinceCode, CancellationToken ct)
    {
        await ActorAsync(null, ct);
        return (await db.Wards.AsNoTracking().Where(x => x.ProvinceCode == provinceCode && db.Datasets.Any(d => d.Id == x.DatasetId && d.IsActive)).OrderBy(x => x.Code).ToListAsync(ct)).Select(WardResponse.From).ToList();
    }

    public async Task<AdministrativeDatasetResponse> ImportAsync(ImportDatasetRequest request, CancellationToken ct)
    {
        await AdminAsync(ct);
        request.Source = Required(request.Source, 500, "source");
        request.Version = Required(request.Version, 100, "version");
        request.Checksum = Required(request.Checksum, 64, "checksum").ToLowerInvariant();
        Check(request.Checksum.Length == 64 && request.Checksum.All(Uri.IsHexDigit), "checksum", "Checksum phải là SHA-256 gồm 64 ký tự hex.");
        Optional(request.SourceCommit, 100, "sourceCommit"); Optional(request.License, 500, "license"); Optional(request.Artifact, 1000, "artifact");
        Check(request.Provinces is { Count: > 0 and <= 1000 } && request.Wards is { Count: > 0 and <= 50000 }, "provinces", "Danh mục trống hoặc quá giới hạn.");
        var provinceCodes = new HashSet<string>(StringComparer.Ordinal);
        var wardCodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in request.Provinces!)
        {
            Check(p is not null, "provinces", "Dữ liệu tỉnh không hợp lệ.");
            p!.Code = Required(p.Code, 20, "provinceCode"); p.Name = Required(p.Name, 255, "provinceName");
            Check(provinceCodes.Add(p.Code), "provinceCode", "Mã tỉnh bị trùng.");
        }
        foreach (var w in request.Wards!)
        {
            Check(w is not null, "wards", "Dữ liệu phường không hợp lệ.");
            w!.Code = Required(w.Code, 20, "wardCode"); w.Name = Required(w.Name, 255, "wardName");
            w.ProvinceCode = Required(w.ProvinceCode, 20, "provinceCode");
            Check(wardCodes.Add(w.Code) && provinceCodes.Contains(w.ProvinceCode), "wardCode", "Phường trùng mã hoặc không thuộc tỉnh trong danh mục.");
        }
        // Hash the normalized content separately: the source checksum alone cannot attest client JSON.
        var content = JsonSerializer.Serialize(new { provinces = request.Provinces.OrderBy(x => x.Code), wards = request.Wards.OrderBy(x => x.Code) });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var existing = await db.Datasets.AsNoTracking().SingleOrDefaultAsync(x => x.Source == request.Source && x.Version == request.Version, ct);
        if (existing is not null)
        {
            if (existing.Checksum != request.Checksum || existing.ContentHash != hash) throw new ConflictException("Phiên bản nguồn đã tồn tại với nội dung khác.");
            await transaction.CommitAsync(ct);
            return AdministrativeDatasetResponse.From(existing);
        }
        var dataset = new AdministrativeDataset
        {
            Source = request.Source,
            Version = request.Version,
            Checksum = request.Checksum,
            ContentHash = hash,
            SourceCommit = request.SourceCommit,
            License = request.License,
            Artifact = request.Artifact
        };
        db.Datasets.Add(dataset);
        db.Provinces.AddRange(request.Provinces.Select(x => new Province { DatasetId = dataset.Id, Code = x.Code, Name = x.Name }));
        db.Wards.AddRange(request.Wards.Select(x => new Ward { DatasetId = dataset.Id, Code = x.Code, ProvinceCode = x.ProvinceCode, Name = x.Name }));
        await SaveAsync(ct);
        await db.Datasets.Where(x => x.IsActive).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
        dataset.IsActive = true;
        await SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return AdministrativeDatasetResponse.From(dataset);
    }

    private async Task AddressValidAsync(Guid datasetId, string province, string ward, string detail, string? instructions,
        decimal? latitude, decimal? longitude, bool precise, CancellationToken ct, bool requireActive = true)
    {
        Required(province, 20, "provinceCode"); Required(ward, 20, "wardCode"); Required(detail, 500, "addressDetail");
        Optional(instructions, 1000, "accessInstructions");
        Check(latitude.HasValue == longitude.HasValue, "latitude", "Cần cung cấp cả latitude và longitude.");
        Check(latitude is null or >= -90 and <= 90, "latitude", "Latitude phải từ -90 đến 90.");
        Check(longitude is null or >= -180 and <= 180, "longitude", "Longitude phải từ -180 đến 180.");
        Check(!precise || latitude.HasValue, "latitude", "Địa điểm này bắt buộc có tọa độ.");
        Check(await db.Wards.AnyAsync(x => x.DatasetId == datasetId && x.ProvinceCode == province && x.Code == ward, ct), "wardCode", "Tỉnh/phường không thuộc cùng phiên bản danh mục.");
        if (requireActive) Check(await db.Datasets.AnyAsync(x => x.Id == datasetId && x.IsActive, ct), "datasetId", "Danh mục này không còn dùng cho lựa chọn mới.");
    }

    public async Task<IReadOnlyList<CustomerAddressResponse>> AddressesAsync(CancellationToken ct)
    {
        var actor = await ActorAsync(AccountGroups.Customer, ct);
        return (await db.Addresses.AsNoTracking().Where(x => x.CustomerId == actor && x.IsActive).OrderBy(x => x.Id).ToListAsync(ct)).Select(CustomerAddressResponse.From).ToList();
    }

    public async Task<CustomerAddressResponse> AddressAsync(Guid id, CancellationToken ct)
        => CustomerAddressResponse.From(await FetchAddressAsync(id, ct));

    private async Task<CustomerAddress> FetchAddressAsync(Guid id, CancellationToken ct)
    {
        var actor = await ActorAsync(AccountGroups.Customer, ct);
        return await db.Addresses.SingleOrDefaultAsync(x => x.Id == id && x.CustomerId == actor && x.IsActive, ct)
            ?? throw new NotFoundException("Không tìm thấy địa chỉ.");
    }

    public async Task<CustomerAddressResponse> SaveAddressAsync(Guid? id, SaveAddressRequest request, CancellationToken ct)
    {
        var actor = await ActorAsync(AccountGroups.Customer, ct);
        var address = id.HasValue ? await FetchAddressAsync(id.Value, ct) : new CustomerAddress { CustomerId = actor };
        if (id.HasValue) Version(request.Version, address.Version);
        var changed = address.DatasetId != request.DatasetId || address.ProvinceCode != request.ProvinceCode || address.WardCode != request.WardCode
            || address.AddressDetail != request.AddressDetail || address.AccessInstructions != request.AccessInstructions || address.Latitude != request.Latitude || address.Longitude != request.Longitude;
        await AddressValidAsync(request.DatasetId, request.ProvinceCode, request.WardCode, request.AddressDetail, request.AccessInstructions, request.Latitude, request.Longitude, false, ct, !id.HasValue || changed);
        Optional(request.Label, 100, "label");
        Check(request.ConfirmationMethod is null or "MapPin" or "ManualCoordinates", "confirmationMethod", "Cách xác nhận vị trí không hợp lệ.");
        Check(request.ConfirmationMethod is null || request.Latitude.HasValue, "confirmationMethod", "Cần có tọa độ trước khi xác nhận.");
        address.DatasetId = request.DatasetId; address.ProvinceCode = request.ProvinceCode; address.WardCode = request.WardCode;
        address.AddressDetail = request.AddressDetail.Trim(); address.AccessInstructions = request.AccessInstructions; address.Label = request.Label;
        address.Latitude = request.Latitude; address.Longitude = request.Longitude;
        if (changed) { address.ConfirmationMethod = null; address.ConfirmedAt = null; address.ConfirmedBy = null; }
        if (request.ConfirmationMethod is not null) { address.ConfirmationMethod = request.ConfirmationMethod; address.ConfirmedAt = DateTime.UtcNow; address.ConfirmedBy = actor; }
        if (id.HasValue) address.Version++; else db.Addresses.Add(address);
        await SaveAsync(ct);
        return CustomerAddressResponse.From(address);
    }

    public async Task ArchiveAddressAsync(Guid id, int version, CancellationToken ct)
    {
        var address = await FetchAddressAsync(id, ct);
        Version(version, address.Version); address.IsActive = false; address.Version++;
        await SaveAsync(ct);
    }

    public async Task<PagedResult<SharedLocationResponse>> SharedAsync(string? search, string? provinceCode, string? type, int pageNumber, int pageSize, CancellationToken ct)
        => await QuerySharedAsync(search, provinceCode, type, pageNumber, pageSize, false, ct);

    public async Task<PagedResult<SharedLocationResponse>> AdminSharedAsync(string? search, int pageNumber, int pageSize, CancellationToken ct)
    {
        await AdminAsync(ct);
        return await QuerySharedAsync(search, null, null, pageNumber, pageSize, true, ct);
    }

    private async Task<PagedResult<SharedLocationResponse>> QuerySharedAsync(string? search, string? provinceCode, string? type, int pageNumber, int pageSize, bool includeInactive, CancellationToken ct)
    {
        await ActorAsync(null, ct);
        Check(pageNumber > 0 && pageNumber <= 100000 && pageSize > 0 && pageSize <= 100, "pageSize", "Phân trang không hợp lệ (1–100 bản ghi).");
        Optional(search, 100, "search");
        var query = db.SharedLocations.AsNoTracking().Where(x => includeInactive || x.IsActive);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search) || x.AddressDetail.Contains(search));
        if (!string.IsNullOrEmpty(provinceCode)) query = query.Where(x => x.ProvinceCode == provinceCode);
        if (!string.IsNullOrEmpty(type)) query = query.Where(x => x.Type == type);
        var count = await query.CountAsync(ct);
        return new((await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct)).Select(SharedLocationResponse.From).ToList(), pageNumber, pageSize, count);
    }

    public async Task<SharedLocationResponse> SharedDetailAsync(Guid id, CancellationToken ct)
    {
        await ActorAsync(null, ct);
        return SharedLocationResponse.From(await db.SharedLocations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct) ?? throw new NotFoundException("Không tìm thấy địa điểm đang sử dụng."));
    }

    private async Task SharedValidAsync(SaveSharedLocationRequest request, CancellationToken ct)
    {
        Required(request.Name, 255, "name"); Required(request.Type, 50, "type"); Required(request.Reason, 1000, "reason");
        Check(request.ConfirmationMethod is "MapPin" or "ManualCoordinates", "confirmationMethod", "Bạn phải xác nhận vị trí của địa điểm.");
        await AddressValidAsync(request.DatasetId, request.ProvinceCode, request.WardCode, request.AddressDetail, request.AccessInstructions, request.Latitude, request.Longitude, true, ct);
    }

    private static void Apply(SharedLocation location, SaveSharedLocationRequest r)
    {
        location.Name = r.Name.Trim(); location.Type = r.Type.Trim(); location.DatasetId = r.DatasetId; location.ProvinceCode = r.ProvinceCode;
        location.WardCode = r.WardCode; location.AddressDetail = r.AddressDetail.Trim(); location.AccessInstructions = r.AccessInstructions;
        location.Latitude = r.Latitude; location.Longitude = r.Longitude;
        location.ConfirmationMethod = r.ConfirmationMethod;
    }

    private void Record(SharedLocation l, Guid actor, string reason) => db.LocationVersions.Add(new SharedLocationVersion
    {
        LocationId = l.Id,
        Version = l.Version,
        Name = l.Name,
        Type = l.Type,
        DatasetId = l.DatasetId,
        ProvinceCode = l.ProvinceCode,
        WardCode = l.WardCode,
        AddressDetail = l.AddressDetail,
        AccessInstructions = l.AccessInstructions,
        Latitude = l.Latitude,
        Longitude = l.Longitude,
        IsActive = l.IsActive,
        ChangedBy = actor,
        Reason = reason.Trim(),
        ConfirmationMethod = l.ConfirmationMethod,
        ConfirmedAt = l.ConfirmedAt,
        ConfirmedBy = l.ConfirmedBy
    });

    private async Task<SharedLocation> ChangeSharedAsync(Guid? id, SaveSharedLocationRequest request, Guid actor, CancellationToken ct)
    {
        await SharedValidAsync(request, ct);
        var location = id.HasValue ? await db.SharedLocations.SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct)
            ?? throw new NotFoundException("Không tìm thấy địa điểm đang sử dụng.") : new SharedLocation();
        if (id.HasValue) { Version(request.Version, location.Version); location.Version++; } else db.SharedLocations.Add(location);
        Apply(location, request); location.ConfirmedAt = DateTime.UtcNow; location.ConfirmedBy = actor;
        Record(location, actor, request.Reason);
        return location;
    }

    public async Task<SharedLocationResponse> SaveSharedAsync(Guid? id, SaveSharedLocationRequest request, CancellationToken ct)
    {
        var actor = await AdminAsync(ct);
        var result = await ChangeSharedAsync(id, request, actor, ct);
        await SaveAsync(ct);
        return SharedLocationResponse.From(result);
    }

    private async Task ArchiveSharedCoreAsync(Guid id, int version, string reason, Guid actor, CancellationToken ct)
    {
        Required(reason, 1000, "reason");
        var location = await db.SharedLocations.SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct) ?? throw new NotFoundException("Không tìm thấy địa điểm đang sử dụng.");
        Version(version, location.Version); location.IsActive = false; location.Version++; Record(location, actor, reason);
    }

    public async Task ArchiveSharedAsync(Guid id, ArchiveLocationRequest request, CancellationToken ct)
    {
        await ArchiveSharedCoreAsync(id, request.Version, request.Reason, await AdminAsync(ct), ct);
        await SaveAsync(ct);
    }

    public async Task<IReadOnlyList<SharedLocationVersionResponse>> HistoryAsync(Guid id, CancellationToken ct)
    {
        await AdminAsync(ct);
        return (await db.LocationVersions.AsNoTracking().Where(x => x.LocationId == id).OrderByDescending(x => x.Version).ToListAsync(ct)).Select(SharedLocationVersionResponse.From).ToList();
    }

    public async Task<IReadOnlyList<LocationChangeRequestResponse>> RequestsAsync(Guid? organizationId, string? status, bool admin, CancellationToken ct)
    {
        var query = db.Requests.AsNoTracking();
        if (admin) await AdminAsync(ct);
        else
        {
            var actor = await ActorAsync(AccountGroups.Operator, ct);
            Check(organizationId.HasValue, "organizationId", "Cần chọn đơn vị vận tải.");
            await organizations.RequireOwnerAsync(organizationId!.Value, actor, ct);
            query = query.Where(x => x.OrganizationId == organizationId);
        }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return (await query.OrderByDescending(x => x.SubmittedAt).Take(200).ToListAsync(ct)).Select(LocationChangeRequestResponse.From).ToList();
    }

    public async Task<LocationChangeRequestResponse> SubmitAsync(SubmitLocationRequest request, CancellationToken ct)
    {
        var actor = await ActorAsync(AccountGroups.Operator, ct);
        await organizations.RequireOwnerAsync(request.OrganizationId, actor, ct);
        Check(request.Kind is "Create" or "Update" or "Archive", "kind", "Loại yêu cầu không hợp lệ.");
        Required(request.Reason, 1000, "reason");
        if (request.Kind == "Create") Check(request.LocationId is null && request.BaseVersion is null, "locationId", "Đề xuất mới không tham chiếu điểm cũ.");
        else
        {
            Check(request.LocationId.HasValue && request.BaseVersion.HasValue, "locationId", "Cần địa điểm và phiên bản gốc.");
            var location = await SharedDetailAsync(request.LocationId!.Value, ct);
            Version(request.BaseVersion!.Value, location.Version);
        }
        if (request.Kind != "Archive") await SharedValidAsync(ToShared(request), ct);
        var row = new LocationChangeRequest
        {
            OrganizationId = request.OrganizationId,
            SubmittedBy = actor,
            Kind = request.Kind,
            LocationId = request.LocationId,
            BaseVersion = request.BaseVersion,
            Name = request.Name,
            Type = request.Type,
            DatasetId = request.DatasetId,
            ProvinceCode = request.ProvinceCode,
            WardCode = request.WardCode,
            AddressDetail = request.AddressDetail,
            AccessInstructions = request.AccessInstructions,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Reason = request.Reason.Trim(),
            ConfirmationMethod = request.Kind == "Archive" ? null : request.ConfirmationMethod,
            ConfirmedAt = request.Kind == "Archive" ? null : DateTime.UtcNow,
            ConfirmedBy = request.Kind == "Archive" ? null : actor
        };
        db.Requests.Add(row); await SaveAsync(ct); return LocationChangeRequestResponse.From(row);
    }

    private static SaveSharedLocationRequest ToShared(SubmitLocationRequest r) => new()
    {
        Name = r.Name,
        Type = r.Type,
        DatasetId = r.DatasetId,
        ProvinceCode = r.ProvinceCode,
        WardCode = r.WardCode,
        AddressDetail = r.AddressDetail,
        AccessInstructions = r.AccessInstructions,
        Latitude = r.Latitude,
        Longitude = r.Longitude,
        Reason = r.Reason,
        Version = r.BaseVersion ?? 0,
        ConfirmationMethod = r.ConfirmationMethod
    };

    public async Task<LocationChangeRequestResponse> ReviewAsync(Guid id, ReviewLocationRequest request, CancellationToken ct)
    {
        var actor = await AdminAsync(ct);
        Required(request.Reason, 1000, "reason");
        var row = await db.Requests.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Không tìm thấy đề xuất.");
        Version(request.Version, row.Version);
        if (row.Status != "Pending") throw new ConflictException("Đề xuất đã được xét duyệt.");
        if (request.Approve)
        {
            if (row.Kind == "Archive") await ArchiveSharedCoreAsync(row.LocationId!.Value, row.BaseVersion!.Value, request.Reason, actor, ct);
            else
            {
                var location = await ChangeSharedAsync(row.Kind == "Create" ? null : row.LocationId, new SaveSharedLocationRequest
                {
                    Name = row.Name,
                    Type = row.Type,
                    DatasetId = row.DatasetId,
                    ProvinceCode = row.ProvinceCode,
                    WardCode = row.WardCode,
                    AddressDetail = row.AddressDetail,
                    AccessInstructions = row.AccessInstructions,
                    Latitude = row.Latitude,
                    Longitude = row.Longitude,
                    Reason = request.Reason,
                    Version = row.BaseVersion ?? 0,
                    ConfirmationMethod = row.ConfirmationMethod
                }, actor, ct);
                row.LocationId = location.Id;
            }
        }
        row.Status = request.Approve ? "Approved" : "Rejected"; row.ReviewedAt = DateTime.UtcNow; row.ReviewedBy = actor;
        row.ReviewReason = request.Reason.Trim(); row.Version++;
        // One SaveChanges transaction atomically writes the decision, current version and immutable history.
        await SaveAsync(ct); return LocationChangeRequestResponse.From(row);
    }
}
