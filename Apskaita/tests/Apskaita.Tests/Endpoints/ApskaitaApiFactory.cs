using Apskaita.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Apskaita.Tests.Endpoints;

/// <summary>
/// Boots the real Apskaita application in-process for integration tests, replacing the MySQL-backed
/// <see cref="ApskaitaDbContext"/> with a shared EF In-Memory store so endpoints and test seeding
/// operate on the same data.
/// </summary>
public sealed class ApskaitaApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"e2e-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // AddDataAccess registers the MySQL/Microting provider (options action would auto-detect a MySQL
            // server). Remove every EF/MySQL/Microting descriptor it added, then re-register the context
            // cleanly against a shared in-memory store so exactly one database provider remains.
            var toRemove = services
                .Where(d =>
                    (d.ServiceType.FullName?.Contains("EntityFrameworkCore") ?? false) ||
                    (d.ServiceType.FullName?.Contains("MySql") ?? false) ||
                    (d.ImplementationType?.FullName?.Contains("EntityFrameworkCore") ?? false) ||
                    (d.ImplementationType?.FullName?.Contains("MySql") ?? false) ||
                    (d.ImplementationType?.FullName?.Contains("Microting") ?? false) ||
                    (d.ImplementationType?.FullName?.Contains("Pomelo") ?? false))
                .ToList();

            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApskaitaDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }

    /// <summary>
    /// A context bound to the same in-memory store the application uses, for seeding and assertions.
    /// Dispose it after use.
    /// </summary>
    public ApskaitaDbContext NewDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApskaitaDbContext>();
    }
}
