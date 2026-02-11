using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NkplmErp.Application.Interfaces;
using NkplmErp.Domain.Common;
using NkplmErp.Domain.Entities;
using System.Text.Json;

namespace NkplmErp.Infrastructure.Persistence.Interceptors;

public class AuditLoggingInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;
    private List<AuditLog> _tempAuditLogs = new();

    public AuditLoggingInterceptor(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        OnBeforeSaveChanges(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        OnBeforeSaveChanges(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        OnAfterSaveChanges(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        OnAfterSaveChanges(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void OnBeforeSaveChanges(DbContext? context)
    {
        if (context == null) return;

        context.ChangeTracker.DetectChanges();
        _tempAuditLogs.Clear();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                continue;

            var auditEntry = new AuditEntry(entry);
            auditEntry.TableName = entry.Metadata.GetTableName();
            auditEntry.UserId = _currentUserService.UserId;
            _tempAuditLogs.Add(auditEntry.ToAudit());
        }
    }

    private void OnAfterSaveChanges(DbContext? context)
    {
        if (context == null || !_tempAuditLogs.Any()) return;

        foreach (var auditLog in _tempAuditLogs)
        {
            context.Set<AuditLog>().Add(auditLog);
        }

        _tempAuditLogs.Clear();
        // We call SaveChanges again to persist the logs. 
        // Caution: This might trigger interceptors again, but we filter out AuditLog entity in OnBefore.
        context.SaveChanges();
    }
}

public class AuditEntry
{
    public EntityEntry Entry { get; }
    public string? UserId { get; set; }
    public string? TableName { get; set; }
    public Dictionary<string, object> KeyValues { get; } = new();
    public Dictionary<string, object> OldValues { get; } = new();
    public Dictionary<string, object> NewValues { get; } = new();
    public List<string> ChangedColumns { get; } = new();

    public AuditEntry(EntityEntry entry)
    {
        Entry = entry;
        UpdateAuditProperties();
    }

    private void UpdateAuditProperties()
    {
        foreach (var property in Entry.Properties)
        {
            string propertyName = property.Metadata.Name;
            if (property.Metadata.IsPrimaryKey())
            {
                KeyValues[propertyName] = property.CurrentValue!;
                continue;
            }

            switch (Entry.State)
            {
                case EntityState.Added:
                    NewValues[propertyName] = property.CurrentValue!;
                    break;

                case EntityState.Deleted:
                    OldValues[propertyName] = property.OriginalValue!;
                    break;

                case EntityState.Modified:
                    if (property.IsModified)
                    {
                        ChangedColumns.Add(propertyName);
                        OldValues[propertyName] = property.OriginalValue!;
                        NewValues[propertyName] = property.CurrentValue!;
                    }
                    break;
            }
        }
    }

    public AuditLog ToAudit()
    {
        var audit = new AuditLog();
        audit.UserId = UserId;
        audit.Type = Entry.State.ToString();
        audit.TableName = TableName;
        audit.DateTime = DateTime.UtcNow;
        audit.PrimaryKey = JsonSerializer.Serialize(KeyValues);
        audit.OldValues = OldValues.Count == 0 ? null : JsonSerializer.Serialize(OldValues);
        audit.NewValues = NewValues.Count == 0 ? null : JsonSerializer.Serialize(NewValues);
        audit.AffectedColumns = ChangedColumns.Count == 0 ? null : JsonSerializer.Serialize(ChangedColumns);
        return audit;
    }
}
