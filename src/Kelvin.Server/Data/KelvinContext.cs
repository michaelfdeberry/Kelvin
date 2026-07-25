using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Data;

public class KelvinContext(DbContextOptions<KelvinContext> options) : DbContext(options)
{
  public DbSet<Gateway> Gateways => Set<Gateway>();

  public DbSet<Sensor> Sensors => Set<Sensor>();

  public DbSet<SensorPacket> SensorPackets => Set<SensorPacket>();

  public DbSet<Preferences> Preferences => Set<Preferences>();

  public DbSet<Thermostat> Thermostats => Set<Thermostat>();

  public DbSet<SetPoint> SetPoints => Set<SetPoint>();

  public DbSet<Schedule> Schedules => Set<Schedule>();

  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
  {
    base.OnConfiguring(optionsBuilder);
    optionsBuilder.AddInterceptors(new EntityUpdateInterceptor());
  }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    var entityTypes = typeof(Entity)
      .Assembly.GetTypes()
      .Where(type => type != typeof(Entity) && typeof(Entity).IsAssignableFrom(type) && type.IsClass && !type.IsAbstract)
      .Where(type => string.Equals(type.Namespace, typeof(Entity).Namespace, StringComparison.Ordinal))
      .OrderBy(type => type.Name)
      .ToList();

    foreach (var entityType in entityTypes)
    {
      modelBuilder.Entity(entityType);
    }
  }
}
