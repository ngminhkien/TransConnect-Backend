using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Entities;

namespace TransConnect.Data.Repositories;

// TContext là DbContext cụ thể của từng module (vd IdentityDbContext) — mỗi module
// tự đăng ký Repository<TEntity, TheirDbContext> cho các entity của mình.
public class Repository<T, TContext>(TContext context) : IRepository<T>
    where T : BaseEntity
    where TContext : DbContext
{
    protected readonly TContext Context = context;
    protected DbSet<T> Set => Context.Set<T>();

    // Cố tình KHÔNG dùng Set.FindAsync: FindAsync tra theo khoá chính và không áp global
    // query filter, nên sẽ trả về cả bản ghi đã soft-delete.
    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await Set.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await Set.ToListAsync(cancellationToken);

    public IQueryable<T> Query() => Set.AsQueryable();

    public IQueryable<T> QueryIncludingDeleted() => Set.IgnoreQueryFilters();

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default) =>
        await Set.AddAsync(entity, cancellationToken);

    public void Update(T entity) => Set.Update(entity);

    public void Remove(T entity)
    {
        entity.DeletedAt = DateTime.UtcNow;
        Set.Update(entity);
    }

    public void HardRemove(T entity) => Set.Remove(entity);
}
