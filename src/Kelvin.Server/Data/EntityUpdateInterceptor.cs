using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kelvin.Server.Data;

/// <summary>
/// Stamps the <see cref="Entity" /> audit timestamps from the registered <see cref="TimeProvider" /> and turns
/// deletions into soft deletes.
/// </summary>
/// <remarks>
/// The timestamps are always overwritten rather than only filled when unset, so no caller can smuggle in a
/// wall-clock value and the whole application shares one driveable clock.
/// </remarks>
public sealed class EntityUpdateInterceptor(TimeProvider time) : SaveChangesInterceptor
{
  public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
  {
    UpdateEntities(eventData.Context);
    return base.SavingChanges(eventData, result);
  }

  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
    DbContextEventData eventData,
    InterceptionResult<int> result,
    CancellationToken cancellationToken = default
  )
  {
    UpdateEntities(eventData.Context);
    return new ValueTask<InterceptionResult<int>>(result);
  }

  private void UpdateEntities(DbContext? context)
  {
    if (context is null)
    {
      return;
    }

    var now = time.GetUtcNow();

    foreach (var entry in context.ChangeTracker.Entries<Entity>())
    {
      switch (entry.State)
      {
        case EntityState.Added:
          entry.Entity.CreatedAt = now;
          entry.Entity.UpdatedAt = now;
          break;
        case EntityState.Modified:
          entry.Entity.UpdatedAt = now;
          break;
        case EntityState.Deleted:
          // Nothing is ever removed from the database; a delete becomes an update that stamps DeletedAt.
          entry.State = EntityState.Modified;
          entry.Entity.UpdatedAt = now;
          entry.Entity.DeletedAt = now;
          break;
      }
    }
  }
}
