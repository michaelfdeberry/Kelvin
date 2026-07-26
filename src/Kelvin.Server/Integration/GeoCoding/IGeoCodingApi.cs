using Kelvin.Server.Models;

namespace Kelvin.Server.Integration.GeoCoding;

public interface IGeoCodingApi
{
  Task<IReadOnlyList<GeoCodingLocation>> SearchAsync(string name, int count = 10, CancellationToken cancellationToken = default);

  Task<GeoCodingLocation?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
}
