namespace Kelvin.Server.Application;

/// <summary>
/// Represents a mapper for endpoints. Implement this interface in a class to map endpoints for a specific feature.
/// </summary>
public interface IEndpointMapper
{
  void MapEndpoint(IEndpointRouteBuilder app);
}

/// <summary>
/// Provides extension methods for mapping endpoints in the application.
/// </summary>
public static class EndpointMapperExtensions
{
  public static void MapEndpoints(this IEndpointRouteBuilder app)
  {
    typeof(IEndpointMapper)
      .Assembly.GetTypes()
      .Where(t => typeof(IEndpointMapper).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
      .Select(Activator.CreateInstance)
      .Cast<IEndpointMapper>()
      .ToList()
      .ForEach(mapper => mapper.MapEndpoint(app));
  }
}
