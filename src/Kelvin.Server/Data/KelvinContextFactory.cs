using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Kelvin.Server.Data;

public class KelvinContextFactory : IDesignTimeDbContextFactory<KelvinContext>
{
  public KelvinContext CreateDbContext(string[] args)
  {
    var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

    var configuration = new ConfigurationBuilder()
      .SetBasePath(Directory.GetCurrentDirectory())
      .AddJsonFile("appsettings.json", optional: true)
      .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
      .AddEnvironmentVariables()
      .Build();

    var optionsBuilder = new DbContextOptionsBuilder<KelvinContext>();
    optionsBuilder.UseSqlite(KelvinContext.ResolveSqliteConnectionString(configuration));

    return new KelvinContext(optionsBuilder.Options);
  }
}
