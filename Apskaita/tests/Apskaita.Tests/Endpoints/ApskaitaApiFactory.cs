using Apskaita.Entities;
using Apskaita.Feature.Account;
using Apskaita.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Apskaita.Tests.Endpoints;

/// <summary>
/// Boots the real Apskaita application in-process for integration tests, replacing the MySQL-backed
/// <see cref="ApskaitaDbContext"/> with a shared EF In-Memory store and the real authentication stack with
/// a stub handler (<see cref="TestStubHandler"/>) that authenticates a configurable principal
/// (default: admin).
/// </summary>
public class ApskaitaApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"e2e-{Guid.NewGuid():N}";

    /// <summary>The principal the stub handler authenticates. Defaults to an admin user.</summary>
    public TestPrincipalProvider Principals { get; } = new() { Principal = TestPrincipal.Build("admin@example.com", UserRole.Admin) };

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // AddDataAccess registers the MySQL/Microting provider (options action would auto-detect a MySQL
            // server). Remove every EF/MySQL/Microting descriptor it added, then re-register the context
            // cleanly against a shared in-memory store so exactly one database provider remains.
            // Data Protection's EF store (namespace ...DataProtection.EntityFrameworkCore) must survive —
            // it is provider-agnostic and keeps the key ring working under the in-memory context.
            var toRemove = services
                .Where(d =>
                    ((d.ServiceType.FullName?.Contains("EntityFrameworkCore") ?? false) ||
                    (d.ServiceType.FullName?.Contains("MySql") ?? false) ||
                    (d.ImplementationType?.FullName?.Contains("EntityFrameworkCore") ?? false) ||
                    (d.ImplementationType?.FullName?.Contains("MySql") ?? false) ||
                    (d.ImplementationType?.FullName?.Contains("Microting") ?? false) ||
                    (d.ImplementationType?.FullName?.Contains("Pomelo") ?? false))
                    && !(d.ServiceType.FullName?.Contains("DataProtection") ?? false)
                    && !(d.ImplementationType?.FullName?.Contains("DataProtection") ?? false))
                .ToList();

            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApskaitaDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            // Replace authentication: the stub scheme authenticates the configurable principal while the
            // cookie scheme stays as the challenge target, so unauthenticated requests still redirect to
            // /account/login (and emit HX-Redirect for HTMX calls) exactly as in production.
            services.AddSingleton(Principals);
            services.AddAuthentication(auth =>
            {
                auth.DefaultScheme = TestStubHandler.SchemeName;
                auth.DefaultAuthenticateScheme = TestStubHandler.SchemeName;
                auth.DefaultChallengeScheme = AuthConstants.CookieScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestStubHandler>(TestStubHandler.SchemeName, null);
        });
    }

    /// <summary>Authenticates subsequent requests as the given email/role (null role = roleless user).</summary>
    public void SetPrincipal(string email, UserRole? role, Guid? userId = null) => Principals.Principal = TestPrincipal.Build(email, role, userId);

    /// <summary>Makes subsequent requests unauthenticated.</summary>
    public void SetAnonymous() => Principals.Principal = null;

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
