using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Data;

public static class KelvinDataExtension
{
  public static void SoftDelete<T>(this DbSet<T> set, T entity)
    where T : Entity, new()
  {
    entity.DeletedAt = DateTime.UtcNow;
  }

  public static void SoftDelete(this KelvinContext context, object entity)
  {
    if (entity is Entity e)
    {
      e.DeletedAt = DateTime.UtcNow;
    }
  }
}
