using Apskaita.Web;
using Apskaita.Web.Data;
using Serilog;

// Migration runner mode (replaces the retired Apskaita.Data.Migration project): applies pending
// EF Core migrations against MySQL and exits without starting the host, e.g.
//   dotnet run --project src/Apskaita.Web -- --migrate --connection=...
// The web host itself never migrates on startup, so in-process test hosts are unaffected.
if (args.Any(a => a.Equals("--migrate", StringComparison.OrdinalIgnoreCase)))
{
    await MigrationRunner.RunAsync(args);
    return;
}

// Startup configuration is decomposed into the Setup class (see Setup.cs); this file is intentionally a
// thin orchestrator so the composition and ordering are obvious at a glance.
//
// Serilog is wired per-host through DI: AddSerilog replaces the default ILoggerFactory with a Serilog-
// backed one, so every ILogger/ILoggerFactory consumer (the OIDC handlers, the custom request-logging
// middleware, Setup.Logging, ...) routes through Serilog without code changes. Levels, sinks, and
// destructuring limits live in the "Serilog" section of appsettings*.json (ReadFrom.Configuration).
//
// The two-stage static bootstrap logger (Log.Logger = new LoggerConfiguration()...CreateBootstrapLogger())
// is deliberately NOT used here: WebApplicationFactory<Program> boots multiple hosts in a single process
// for integration tests, and freezing the process-wide static ReloadableLogger from more than one host
// throws "The logger is already frozen". Keeping the logger per-host (DI-scoped) avoids that race.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, lc) => lc
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

Setup.RazorSlices(builder.Services);
Setup.Features(builder.Services);
Setup.CalendarConnectors(builder.Services);
Setup.Validation(builder.Services);
Setup.EfCore(builder.Services, builder.Configuration);
Setup.EntraId(builder.Services, builder.Configuration);
Setup.HttpClientLogging(builder.Services, builder.Configuration);

var app = builder.Build();

// Logging runs immediately after Build: it logs the configured providers and installs the request-logging
// middleware (must be first). The OIDC backchannel/event handlers get their logger factory through DI
// (AddOptions<OpenIdConnectOptions>.Configure<ILoggerFactory>), so nothing is captured from the builder.
Setup.Logging(app);
Setup.Pipeline(app);
Setup.Endpoints(app);

app.Run();

/// <summary>
/// Exposes the top-level-statement entry point type so <c>WebApplicationFactory&lt;Program&gt;</c>
/// can boot the app in integration tests.
/// </summary>
public partial class Program;
