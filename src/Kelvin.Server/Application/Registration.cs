namespace Kelvin.Server.Application;

/// <summary>
/// Marker interface for feature registration. Implement this interface in a class to register services for a specific feature.
/// </summary>
public interface IRegistration
{
  void Register(IServiceCollection services);
}

public static class RegistrationExtensions
{
  /// <summary>
  /// Registers all features that implement IRegistration in the assembly.
  /// </summary>
  public static void RegisterDependencies(this IServiceCollection services)
  {
    typeof(IRegistration)
      .Assembly.GetTypes()
      .Where(t => typeof(IRegistration).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
      .Select(Activator.CreateInstance)
      .Cast<IRegistration>()
      .ToList()
      .ForEach(registration => registration.Register(services));
  }
}
