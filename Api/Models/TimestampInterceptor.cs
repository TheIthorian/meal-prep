using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Api.Models;

/// <summary>
///     Updates entity timestamps during Entity Framework save operations.
/// </summary>
public class TimestampInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result
    ) {
        SetTimestamps(eventData.Context!);
        return base.SavingChanges(eventData, result);
    }

    private static void SetTimestamps(DbContext context) {
        var entries = context.ChangeTracker
            .Entries<Entity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        var now = DateTime.UtcNow;
        foreach (var entry in entries) entry.Property(nameof(Entity.UpdatedAt)).CurrentValue = now;
    }
}
