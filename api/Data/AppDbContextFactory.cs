using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EnergyTracker.Api.Data;

// Required for `dotnet ef` to instantiate AppDbContext during design-time operations
// (migrations, scaffolding) in Azure Functions projects where the host can't be used.
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = ReadConnectionString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new AppDbContext(options);
    }

    private static string ReadConnectionString()
    {
        // 1. Prefer explicit env var (useful in CI)
        var fromEnv = Environment.GetEnvironmentVariable("SqlConnectionString");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        // 2. Fall back to local.settings.json (Azure Functions local dev file)
        // During `dotnet ef`, CWD is the project directory; from bin/Debug/net*/ go up 3 levels.
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "local.settings.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "local.settings.json"),
        };
        var settingsPath = candidates.FirstOrDefault(File.Exists) ?? candidates[0];
        if (File.Exists(settingsPath))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
            if (doc.RootElement.TryGetProperty("Values", out var values) &&
                values.TryGetProperty("SqlConnectionString", out var cs))
            {
                var value = cs.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }

        throw new InvalidOperationException(
            "SqlConnectionString not found. Set it in local.settings.json Values or as an environment variable.");
    }
}
