using api.Tests.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace api.Tests.Integration;

// Real SQLite :memory: database with actual EF Core migrations applied (Database.Migrate()),
// not EnsureCreated() — proves the same migration history that would run in production.
// The connection is opened before DbContext construction and kept open for the test's
// lifetime: a :memory: SQLite database is deleted the instant its one connection closes.
public abstract class SqliteIntegrationTestBase : IDisposable
{
    private readonly SqliteConnection _connection;
    protected readonly DbContextOptions<SqliteAppDbContext> ContextOptions;

    protected SqliteIntegrationTestBase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        ContextOptions = new DbContextOptionsBuilder<SqliteAppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.Migrate();
    }

    protected SqliteAppDbContext CreateContext() => new(ContextOptions);

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
