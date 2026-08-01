using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace api.Tests.Data;

// Required for `dotnet ef migrations add --project api.Tests --startup-project api.Tests
// --context SqliteAppDbContext` design-time tooling. Never invoked by the running Function App
// or by `dotnet ef database update` against a real target — SqliteAppDbContext is test-only.
public class SqliteAppDbContextFactory : IDesignTimeDbContextFactory<SqliteAppDbContext>
{
    public SqliteAppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SqliteAppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        return new SqliteAppDbContext(options);
    }
}
