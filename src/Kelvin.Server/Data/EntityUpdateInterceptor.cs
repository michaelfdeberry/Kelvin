using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kelvin.Server.Data;

public sealed class EntityUpdateInterceptor : SaveChangesInterceptor
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

  private static void UpdateEntities(DbContext? context)
  {
    if (context is null)
    {
      return;
    }

    var now = DateTime.UtcNow;

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
      }
    }
  }
}
