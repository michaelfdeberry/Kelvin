namespace Kelvin.Server.Models;

/// <summary>
/// Base class for every persisted entity.
/// </summary>
/// <remarks>
/// The timestamps are owned exclusively by <c>EntityUpdateInterceptor</c>, which stamps them from the registered
/// <see cref="TimeProvider" /> when changes are saved. Nothing else should assign them - a value set by a caller
/// is overwritten - so the clock stays driveable from tests.
/// </remarks>
public abstract class Entity
{
  public Guid Id { get; set; }

  public DateTimeOffset CreatedAt { get; set; }

  public DateTimeOffset? UpdatedAt { get; set; }

  public DateTimeOffset? DeletedAt { get; set; }
}
