using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Data;

public static class DataAccessServiceCollectionExtensions
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, string? connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<ApskaitaDbContext>(options => options
            .UseSnakeCaseNamingConvention()
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), sql => sql.MigrationsAssembly("Apskaita.Web")));
        return services;
    }
}
