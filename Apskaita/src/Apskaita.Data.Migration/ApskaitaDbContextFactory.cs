using Apskaita.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;


namespace Apskaita.Data.Migrations;

public sealed class ApskaitaDbContextFactory : IDesignTimeDbContextFactory<ApskaitaDbContext>
{
    private const string DefaultConnectionString = "Server=127.0.0.1;Port=3306;Database=apskaita;Uid=root;Pwd=root;SslMode=None;AllowPublicKeyRetrieval=True;Allow User Variables=true;";

    public static ApskaitaDbContext Create(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApskaitaDbContext>();
        optionsBuilder
            .UseSnakeCaseNamingConvention()
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), sql => sql.MigrationsAssembly("Apskaita.Data.Migration"));
        return new ApskaitaDbContext(optionsBuilder.Options);
    }

    public ApskaitaDbContext CreateDbContext(string[] args)
    {
        var connectionString = DefaultConnectionString;

        foreach (var arg in args)
        {
            if (arg.StartsWith("--connection=", StringComparison.OrdinalIgnoreCase))
            {
                connectionString = arg[("--connection=".Length)..];
            }
        }

        return Create(connectionString);
    }
}