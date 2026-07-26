using Kelvin.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Kelvin.Server.Tests.TestHelpers;

/// <summary>
/// Provides a real <see cref="KelvinContext"/> backed by an in-memory SQLite database, wired to a
/// <see cref="FakeTimeProvider"/> so the audit timestamps the interceptor stamps are driveable.
/// </summary>
/// <remarks>
/// SQLite rather than the EF in-memory provider because the handlers under test rely on real relational
/// translation - ordering, paging and date comparisons all behave differently under a LINQ-to-objects fake.
/// <para>
/// The connection is held open for the lifetime of the harness: an in-memory SQLite database only exists while
/// at least one connection to it is open, so letting EF close it between operations would discard the schema.
/// Each harness gets its own uniquely named database so tests stay isolated.
/// </para>
/// </remarks>
public sealed class KelvinContextHarness : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<KelvinContext> _options;

    public FakeTimeProvider Time { get; } =
        new(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public KelvinContextHarness()
    {
        _connection = new SqliteConnection(
            $"Data Source=file:{Guid.NewGuid()}?mode=memory&cache=shared"
        );
        _connection.Open();

        _options = new DbContextOptionsBuilder<KelvinContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new EntityUpdateInterceptor(Time))
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>
    /// Creates a context over the shared database. Handlers get a scoped context in production, so each logical
    /// operation in a test should use its own rather than sharing one change tracker across the whole test.
    /// </summary>
    public KelvinContext CreateContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
