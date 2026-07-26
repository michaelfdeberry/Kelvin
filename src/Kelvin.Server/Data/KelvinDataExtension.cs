using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Data;

/// <summary>
/// Soft delete helpers. They only mark the entity for removal - <see cref="EntityUpdateInterceptor" /> turns that
/// into an update that stamps <see cref="Entity.DeletedAt" /> from the registered <see cref="TimeProvider" />.
/// </summary>
public static class KelvinDataExtension
{
  public static void SoftDelete<T>(this DbSet<T> set, T entity)
    where T : Entity, new()
  {
    set.Remove(entity);
  }

  public static void SoftDelete(this KelvinContext context, object entity)
  {
    if (entity is Entity e)
    {
      context.Remove(e);
    }
  }
}
