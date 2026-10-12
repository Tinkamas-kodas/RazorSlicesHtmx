using Apskaita.Entities;
using Apskaita.Web.Features.Account;
using Apskaita.Web.Shared.Calendar;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace Apskaita.Tests.Services;

/// <summary>
/// Unit tests for connector token protection and the Outlook 365 access-token lifecycle (REQ-005):
/// cached-token reuse, refresh on expiry, and failure handling. Graph HTTP calls are stubbed with a
/// fake handler; the store runs against an EF In-Memory database.
/// </summary>
public sealed class Outlook365ConnectorTests
{
    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage>? SendFunc { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult((SendFunc ?? (_ => new HttpResponseMessage(HttpStatusCode.OK)))(request));
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }

    private sealed class NoSessionAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    private static (Outlook365Connector connector, ConnectorTokenProtector protector, ApskaitaDbContext db, FakeHttpHandler handler) Build()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        var sp = services.BuildServiceProvider();
        var protector = new ConnectorTokenProtector(sp.GetRequiredService<IDataProtectionProvider>());

        var db = TestDb.New();
        var handler = new FakeHttpHandler();
        var connector = new Outlook365Connector(
            new CalendarConnectorStore(db, new NoSessionAccessor()),
            protector,
            new FakeHttpClientFactory(handler),
            Options.Create(new ConnectorAuthOptions { ClientId = "client-id", ClientSecret = "client-secret" }),
            NullLogger.Instance);

        return (connector, protector, db, handler);
    }

    private static CalendarConnector Connector(ConnectorTokenProtector protector, string? accessToken = null, DateTime? expiry = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Provider = CalendarProvider.Outlook365,
        AccountEmail = "user@contoso.com",
        RefreshTokenProtected = protector.Protect("refresh-token"),
        AccessTokenProtected = protector.ProtectOptional(accessToken),
        AccessTokenExpiresAtUtc = expiry,
        Scopes = "offline_access",
        ConnectedAtUtc = DateTime.UtcNow
    };

    [Fact]
    public void Protector_RoundTrips_AndRejectsGarbage()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        var sp = services.BuildServiceProvider();
        var protector = new ConnectorTokenProtector(sp.GetRequiredService<IDataProtectionProvider>());

        var protectedToken = protector.Protect("secret-token");
        Assert.True(protectedToken != "secret-token", "Protected payload must differ from plaintext.");
        Assert.True(protector.UnprotectOptional(protectedToken) == "secret-token", "Round-trip should restore the token.");
        Assert.True(protector.UnprotectOptional("garbage") is null, "Undecryptable payload should yield null.");
        Assert.True(protector.ProtectOptional(null) is null && protector.UnprotectOptional(null) is null, "Null stays null.");
    }

    [Fact]
    public async Task AccessToken_Reused_WhileValid()
    {
        var (connector, protector, db, handler) = Build();
        db.CalendarConnectors.Add(Connector(protector, "cached-token", DateTime.UtcNow.AddHours(1)));
        await db.SaveChangesAsync();

        var calls = 0;
        handler.SendFunc = _ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        };

        var token = await connector.GetAccessTokenAsync(db.CalendarConnectors.Single(), CancellationToken.None);

        Assert.True(token == "cached-token", "Valid cached token should be reused.");
        Assert.True(calls == 0, "No token endpoint call expected for a valid cached token.");
    }

    [Fact]
    public async Task AccessToken_Refreshed_WhenExpired()
    {
        var (connector, protector, db, handler) = Build();
        db.CalendarConnectors.Add(Connector(protector, "old-token", DateTime.UtcNow.AddSeconds(-10)));
        await db.SaveChangesAsync();
        var row = db.CalendarConnectors.Single();

        handler.SendFunc = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"access_token":"fresh-token","expires_in":3600,"refresh_token":"rotated-refresh"}""", Encoding.UTF8, "application/json")
        };

        var token = await connector.GetAccessTokenAsync(row, CancellationToken.None);

        Assert.True(token == "fresh-token", "Expired token should be refreshed.");
        Assert.True(protector.UnprotectOptional(row.AccessTokenProtected) == "fresh-token", "New access token should be persisted encrypted.");
        Assert.True(protector.UnprotectOptional(row.RefreshTokenProtected) == "rotated-refresh", "Rotated refresh token should be persisted.");
        Assert.True(row.AccessTokenExpiresAtUtc > DateTime.UtcNow.AddMinutes(50), "Expiry should be pushed out.");
        Assert.True(row.LastSyncError is null, "Successful refresh should clear the error.");
    }

    [Fact]
    public async Task AccessToken_RefreshFailure_RecordsError_AndReturnsNull()
    {
        var (connector, protector, db, handler) = Build();
        db.CalendarConnectors.Add(Connector(protector, null, null));
        await db.SaveChangesAsync();
        var row = db.CalendarConnectors.Single();

        handler.SendFunc = _ => new HttpResponseMessage(HttpStatusCode.BadRequest);

        var token = await connector.GetAccessTokenAsync(row, CancellationToken.None);

        Assert.True(token is null, "Failed refresh should return null.");
        Assert.True(!string.IsNullOrEmpty(row.LastSyncError), "Failure should be recorded on LastSyncError.");
    }

    [Fact]
    public async Task Read_WithoutConnector_ReportsNoConnector()
    {
        var (connector, _, _, _) = Build();

        var result = await connector.ReadAsync(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18), CancellationToken.None);

        Assert.False(result.HasConnector);
        Assert.True(result.Events.Count == 0);
    }

    [Fact]
    public async Task Read_MergesEvents_FromMultipleAccounts()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        var sp = services.BuildServiceProvider();
        var protector = new ConnectorTokenProtector(sp.GetRequiredService<IDataProtectionProvider>());

        var db = TestDb.New();
        var owner = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = owner,
            Email = "user@example.com",
            DisplayName = "User",
            Provider = "entra",
            SubjectId = "s",
            IsActive = true,
            Role = UserRole.User
        });
        db.CalendarConnectors.Add(NewConnector(protector, owner, "a@contoso.com"));
        db.CalendarConnectors.Add(NewConnector(protector, owner, "b@contoso.com"));
        await db.SaveChangesAsync();

        var handler = new FakeHttpHandler();
        var call = 0;
        handler.SendFunc = request =>
        {
            // Token calls first (one per connector), then calendarView calls; each calendarView returns a
            // distinct event so the merge is observable.
            if (request.RequestUri!.ToString().Contains("/oauth2/v2.0/token"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"access_token":"fresh-token","expires_in":3600}""", Encoding.UTF8, "application/json")
                };
            }

            call++;
            var subject = call == 1 ? "Alpha meeting" : "Beta review";
            var start = call == 1 ? "2026-10-13T07:00:00Z" : "2026-10-12T08:00:00Z";
            var end = call == 1 ? "2026-10-13T07:30:00Z" : "2026-10-12T08:15:00Z";
            var json = "{\"value\":[{\"subject\":\"" + subject + "\",\"start\":{\"dateTime\":\"" + start
                + "\",\"timeZone\":\"UTC\"},\"end\":{\"dateTime\":\"" + end + "\",\"timeZone\":\"UTC\"}}]}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        };

        var accessor = new SessionAccessor("user@example.com", owner);
        var connector = new Outlook365Connector(
            new CalendarConnectorStore(db, accessor),
            protector,
            new FakeHttpClientFactory(handler),
            Options.Create(new ConnectorAuthOptions { ClientId = "c", ClientSecret = "s" }),
            NullLogger.Instance);

        var result = await connector.ReadAsync(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18), CancellationToken.None);

        Assert.True(result.HasConnector, "Connectors exist for the session user.");
        Assert.True(result.Events.Count == 2, $"Both accounts should contribute events, got {result.Events.Count}.");
        Assert.True(result.Events[0].Subject == "Beta review", "Merged events are sorted by start.");
        Assert.True(result.Events[1].Subject == "Alpha meeting", "Merged events are sorted by start.");
        Assert.True(result.Notice is null, "No failures expected.");
    }

    private sealed class SessionAccessor(string email, Guid userId, string? cookie = null) : IHttpContextAccessor
    {
        public HttpContext? HttpContext
        {
            get
            {
                var identity = new System.Security.Claims.ClaimsIdentity(
                [
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, email),
                    new System.Security.Claims.Claim(AuthConstants.UserIdClaim, userId.ToString())
                ],
                "Test",
                System.Security.Claims.ClaimTypes.Name,
                System.Security.Claims.ClaimTypes.Role);
                var context = new DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(identity) };
                if (cookie is not null)
                {
                    context.Request.Headers["Cookie"] = cookie;
                }
                return context;
            }
            set { }
        }
    }

    [Fact]
    public async Task Read_ConvertsUtcEvents_ToBrowserCookieOffset()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        var sp = services.BuildServiceProvider();
        var protector = new ConnectorTokenProtector(sp.GetRequiredService<IDataProtectionProvider>());

        var db = TestDb.New();
        var owner = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = owner,
            Email = "user@example.com",
            DisplayName = "User",
            Provider = "entra",
            SubjectId = "s",
            IsActive = true,
            Role = UserRole.User
        });
        db.CalendarConnectors.Add(NewConnector(protector, owner, "a@contoso.com"));
        await db.SaveChangesAsync();

        var handler = new FakeHttpHandler();
        handler.SendFunc = request =>
        {
            if (request.RequestUri!.ToString().Contains("/oauth2/v2.0/token"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"access_token":"fresh-token","expires_in":3600}""", Encoding.UTF8, "application/json")
                };
            }

            // Graph returns "UTC" values offset-less; they must be read as UTC instants, then shown
            // in the browser's zone (cookie tz-offset=180 → UTC+03:00).
            var json = """{"value":[{"subject":"FDMS standup","start":{"dateTime":"2026-10-13T06:45:00.0000000","timeZone":"UTC"},"end":{"dateTime":"2026-10-13T07:00:00.0000000","timeZone":"UTC"}}]}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        };

        var accessor = new SessionAccessor("user@example.com", owner, "tz-offset=180");
        var connector = new Outlook365Connector(
            new CalendarConnectorStore(db, accessor),
            protector,
            new FakeHttpClientFactory(handler),
            Options.Create(new ConnectorAuthOptions { ClientId = "c", ClientSecret = "s" }),
            NullLogger.Instance,
            accessor);

        var result = await connector.ReadAsync(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18), CancellationToken.None);

        Assert.True(result.Events.Count == 1, $"06:45 UTC should land on 13 Oct (+03:00) inside the week, got {result.Events.Count}.");
        var evt = result.Events[0];
        Assert.True(evt.StartLocal == new DateTime(2026, 10, 13, 9, 45, 0), $"06:45 UTC should display as 09:45, got {evt.StartLocal:HH:mm}.");
        Assert.True(evt.EndLocal == new DateTime(2026, 10, 13, 10, 0, 0), $"07:00 UTC should display as 10:00, got {evt.EndLocal:HH:mm}.");
    }

    private static CalendarConnector NewConnector(ConnectorTokenProtector protector, Guid userId, string accountEmail) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Provider = CalendarProvider.Outlook365,
        AccountKey = accountEmail,
        AccountEmail = accountEmail,
        DisplayName = accountEmail,
        RefreshTokenProtected = protector.Protect("refresh-token"),
        Scopes = "offline_access",
        ConnectedAtUtc = DateTime.UtcNow,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };
}
