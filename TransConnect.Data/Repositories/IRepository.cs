using TransConnect.Base.Entities;

namespace TransConnect.Data.Repositories;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    IQueryable<T> Query();

    // Bỏ qua soft-delete filter — dùng khi cần đọc cả bản ghi đã xoá mềm (vd trang khôi phục,
    // báo cáo lịch sử). Mặc định luôn dùng Query()/GetByIdAsync để không lộ dữ liệu đã xoá.
    IQueryable<T> QueryIncludingDeleted();

    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);

    // Xoá mềm: chỉ set DeletedAt, bản ghi vẫn nằm trong DB nhưng bị soft-delete query filter
    // (xem AppDbContextBase) loại khỏi mọi query thông thường.
    void Remove(T entity);

    // Xoá cứng khỏi DB — chỉ dùng khi thực sự cần huỷ dữ liệu (vd yêu cầu xoá dữ liệu cá nhân).
    void HardRemove(T entity);
}
