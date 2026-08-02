using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;

namespace Kelvin.Server.Data;

public class KelvinContext(DbContextOptions<KelvinContext> options) : DbContext(options)
{
  public const string DatabasePathConfigurationKey = "Database:Path";

  public DbSet<Gateway> Gateways => Set<Gateway>();

  public DbSet<Sensor> Sensors => Set<Sensor>();

  public DbSet<SensorPacket> SensorPackets => Set<SensorPacket>();

  public DbSet<Preferences> Preferences => Set<Preferences>();

  public DbSet<Thermostat> Thermostats => Set<Thermostat>();

  public DbSet<SetPoint> SetPoints => Set<SetPoint>();

  public DbSet<Schedule> Schedules => Set<Schedule>();

  public DbSet<ControlStateChange> ControlStateChanges => Set<ControlStateChange>();

  public static string ResolveSqliteConnectionString(IConfiguration configuration)
  {
    var configuredPath = configuration[DatabasePathConfigurationKey];
    var expandedPath = string.IsNullOrWhiteSpace(configuredPath) ? null : System.Environment.ExpandEnvironmentVariables(configuredPath);
    var databasePath = string.IsNullOrWhiteSpace(expandedPath)
      ? Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "kelvin", "kelvin.db")
      : expandedPath;

    var directory = Path.GetDirectoryName(databasePath);
    if (!string.IsNullOrWhiteSpace(directory))
    {
      Directory.CreateDirectory(directory);
    }

    return $"Data Source={databasePath}";
  }

  protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
  {
    base.ConfigureConventions(configurationBuilder);

    // SQLite has no native DateTimeOffset. The provider's default TEXT mapping cannot be ordered or range
    // filtered in SQL, which the history and statistics queries depend on, so store the binary form instead -
    // it is a single integer whose ordering matches the underlying instant.
    configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
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

    // The history is always read as a timeline for one axis over a date range.
    modelBuilder.Entity<ControlStateChange>().HasIndex(change => new { change.Kind, change.CreatedAt });
  }
}
