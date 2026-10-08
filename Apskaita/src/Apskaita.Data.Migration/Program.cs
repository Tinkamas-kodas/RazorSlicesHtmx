using Apskaita.Data.Migrations;
using Microsoft.EntityFrameworkCore;

var options = MigrationOptions.Parse(args);

if (options.ShowHelp)
{
    PrintUsage();
    return 0;
}

await using var db = ApskaitaDbContextFactory.Create(options.ConnectionString);
Console.WriteLine("Applying EF Core migrations...");
await db.Database.MigrateAsync();
Console.WriteLine("Migrations applied.");

return 0;

static void PrintUsage()
{
    Console.WriteLine("Apskaita.Data.Migration");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project src/Apskaita.Data.Migration -- --connection=...");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --connection=...  MySQL connection string.");
    Console.WriteLine("  --help            Show this help.");
}

namespace Apskaita.Data.Migrations
{
    internal sealed record MigrationOptions(string ConnectionString, bool ShowHelp)
    {
        public static MigrationOptions Parse(string[] args)
        {
            var connectionString = "Server=127.0.0.1;Port=3306;Database=apskaita;Uid=root;Pwd=root;SslMode=None;AllowPublicKeyRetrieval=True;Allow User Variables=true;";
            var showHelp = false;

            foreach (var arg in args)
            {
                if (arg is "--help" or "-h" or "/?")
                {
                    showHelp = true;
                    continue;
                }

                if (arg.StartsWith("--connection=", StringComparison.OrdinalIgnoreCase))
                {
                    connectionString = arg[("--connection=".Length)..];
                }
            }

            return new MigrationOptions(connectionString, showHelp);
        }
    }
}