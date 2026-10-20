using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Audit;
using TransConnect.Modules.Identity.Entities;

namespace TransConnect.Modules.Identity.Persistence;

internal static class RbacAuditCapture
{
    public static List<RbacAuditLog> Capture(IdentityDbContext db, IAuditContext? context)
    {
        db.ChangeTracker.DetectChanges();
        if (db.ChangeTracker.Entries<User>().Any(x => x.State == EntityState.Deleted))
            throw new ConflictException("Không được xóa vật lý tài khoản người dùng.");
        var logs = new List<RbacAuditLog>();
        foreach (var entry in db.ChangeTracker.Entries().ToArray())
        {
            string[] fields = entry.Entity switch
            {
                Role => ["Id", "Name", "Description", "DeletedAt"],
                Permission => ["Id", "Code", "Description", "DeletedAt"],
                UserRole => ["UserId", "RoleId"],
                RolePermission => ["RoleId", "PermissionId"],
                _ => []
            };
            if (fields.Length == 0 || entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
            var changes = new Dictionary<string, object?>();
            foreach (var field in fields)
            {
                var property = entry.Property(field);
                if (entry.State == EntityState.Modified && !property.IsModified) continue;
                changes[field] = new
                {
                    Before = entry.State == EntityState.Added ? null : property.OriginalValue,
                    After = entry.State == EntityState.Deleted ? null : property.CurrentValue
                };
            }
            if (changes.Count == 0) continue;
            logs.Add(new RbacAuditLog
            {
                ActorAccountId = context?.ActorAccountId, RequestId = context?.RequestId,
                SubjectType = entry.Metadata.ClrType.Name,
                SubjectId = string.Join(":", entry.Metadata.FindPrimaryKey()!.Properties.Select(p => entry.Property(p.Name).CurrentValue)),
                Action = entry.State.ToString(), ChangesJson = JsonSerializer.Serialize(changes)
            });
        }
        return logs;
    }
}
