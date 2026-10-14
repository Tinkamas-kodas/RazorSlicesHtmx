using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Data;

/// <summary>
/// Applies pending EF Core migrations against MySQL and exits. This is the single migration
/// application entry point (the retired <c>Apskaita.Data.Migration</c> project's role):
/// <c>dotnet run --project src/Apskaita.Web -- --migrate [--connection=...]</c>.
/// The web host itself never migrates on startup, so <c>WebApplicationFactory</c> tests (in-memory
/// provider) are unaffected.
/// </summary>
public static class MigrationRunner
{
    public static async Task RunAsync(string[] args, CancellationToken ct = default)
    {
        var connectionString = ApskaitaDbContextFactory.DefaultConnectionString;
        foreach (var arg in args)
        {
            if (arg.StartsWith("--connection=", StringComparison.OrdinalIgnoreCase))
            {
                connectionString = arg[("--connection=".Length)..];
            }
        }

        await using var db = ApskaitaDbContextFactory.Create(connectionString);
        Console.WriteLine("Applying EF Core migrations...");
        await db.Database.MigrateAsync(ct);
        Console.WriteLine("Migrations applied.");
    }
}
