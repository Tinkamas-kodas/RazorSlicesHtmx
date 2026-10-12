using System.Net;
using System.Text.RegularExpressions;
using Apskaita.Entities;
using Apskaita.Web.Shared.Calendar;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Apskaita.Tests.Endpoints;

/// <summary>
/// Factory that replaces <see cref="ICalendarEventReader"/> with a stub so the time tracking page renders
/// calendar events without Microsoft Graph (PAT-002).
/// </summary>
public sealed class CalendarStubApiFactory : ApskaitaApiFactory
{
    public StubCalendarEventReader Reader { get; } = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(ICalendarEventReader));
            services.Remove(descriptor);
            services.AddSingleton(Reader);
            services.AddSingleton<ICalendarEventReader>(sp => sp.GetRequiredService<StubCalendarEventReader>());
        });
    }
}

/// <summary>Configurable <see cref="ICalendarEventReader"/> stub; default is "no connector".</summary>
public sealed class StubCalendarEventReader : ICalendarEventReader
{
    public CalendarReadResult Result { get; set; } = new(false, [], null);

    public Task<CalendarReadResult> ReadAsync(DateOnly fromLocal, DateOnly toLocal, CancellationToken ct) =>
        Task.FromResult(Result);
}

/// <summary>
/// Integration tests for the calendar connector feature (feature-calendar-connectors-1): Account page
/// states, connect guard, disconnect, and the time tracking calendar panel with a stubbed reader.
/// </summary>
public sealed class CalendarConnectorTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private const string UserEmail = "connector@example.com";

    private static async Task SeedUserAsync(ApskaitaApiFactory factory)
    {
        Guid userId;

        await using (var db = factory.NewDbContext())
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = UserEmail,
                DisplayName = "Connector User",
                Provider = "entra",
                SubjectId = "subject-1",
                IsActive = true,
                Role = UserRole.User
            };

            userId = user.Id;
            db.Users.Add(user);
            await db.SaveChangesAsync(Ct);
        }

        // The connector store resolves its owner from the userId claim, so the test principal must carry it.
        factory.SetPrincipal(UserEmail, UserRole.User, userId);
    }

    private static Guid ConnectorUserId(ApskaitaApiFactory factory)
    {
        using var db = factory.NewDbContext();
        return db.Users.Single(u => u.Email == UserEmail).Id;
    }

    [Fact]
    public async Task AccountRoot_RedirectsToAbsoluteListPath()
    {
        await using var factory = new ApskaitaApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/account");

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, $"Expected 302, got {(int)response.StatusCode}.");
        Assert.Equal("/account/me", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task TimeTrackingRoot_RedirectsToAbsoluteListPath()
    {
        await using var factory = new ApskaitaApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/time-tracking");

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, $"Expected 302, got {(int)response.StatusCode}.");
        Assert.Equal("/time-tracking/list", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task AccountPage_ShowsOutlookCard_NotConnected_AndGooglePlaceholder()
    {
        await using var factory = new ApskaitaApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        var html = await factory.CreateClient().GetStringAsync("/account/me");

        Assert.Contains("Outlook 365 / Microsoft 365", html);
        Assert.Contains("No accounts connected yet.", html);
        Assert.Contains("Coming soon", html);
        Assert.Contains("Google Calendar", html);
        Assert.Contains("/account/connect/outlook365", html);
    }

    private static CalendarConnector NewConnector(Guid userId, string account, string displayName) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Provider = CalendarProvider.Outlook365,
        AccountKey = account,
        AccountEmail = account,
        DisplayName = displayName,
        RefreshTokenProtected = "protected-token",
        Scopes = "https://graph.microsoft.com/Calendars.Read offline_access",
        ConnectedAtUtc = new DateTime(2026, 10, 11, 10, 0, 0, DateTimeKind.Utc),
        CreatedAtUtc = new DateTime(2026, 10, 11, 10, 0, 0, DateTimeKind.Utc),
        UpdatedAtUtc = new DateTime(2026, 10, 11, 10, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task AccountPage_ShowsConnectedState_WhenConnectorExists()
    {
        await using var factory = new ApskaitaApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        var userId = ConnectorUserId(factory);
        Guid connectorId;
        await using (var db = factory.NewDbContext())
        {
            var connector = NewConnector(userId, "outlook-user@contoso.com", "Outlook User");
            connectorId = connector.Id;
            db.CalendarConnectors.Add(connector);
            await db.SaveChangesAsync(Ct);
        }

        var html = await factory.CreateClient().GetStringAsync("/account/me");

        Assert.Contains("Outlook User", html);
        Assert.Contains("outlook-user@contoso.com", html);
        Assert.Contains("Disconnect", html);
        Assert.Contains($"/account/disconnect/{connectorId}", html);
    }

    [Fact]
    public async Task AccountPage_ListsMultipleAccounts_ForSameProvider()
    {
        await using var factory = new ApskaitaApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        var userId = ConnectorUserId(factory);
        await using (var db = factory.NewDbContext())
        {
            db.CalendarConnectors.Add(NewConnector(userId, "dovydas@tinkamaskodas.lt", "Work One"));
            db.CalendarConnectors.Add(NewConnector(userId, "dovydas@nrdcompanies.com", "Work Two"));
            await db.SaveChangesAsync(Ct);
        }

        var html = await factory.CreateClient().GetStringAsync("/account/me");

        Assert.Contains("dovydas@tinkamaskodas.lt", html);
        Assert.Contains("dovydas@nrdcompanies.com", html);
        Assert.Contains("Work One", html);
        Assert.Contains("Work Two", html);
    }

    [Fact]
    public async Task Connect_Outlook365_RedirectsToConsentWithGraphScopes()
    {
        await using var factory = new ApskaitaApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        // Entra is configured in dev (user secrets); the consent scheme challenges to Microsoft with the
        // Graph delegated scopes. No-redirect client so we inspect the 302 itself (AC-002).
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/account/connect/outlook365");

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, $"Expected 302, got {(int)response.StatusCode}.");
        var location = response.Headers.Location!.ToString();
        Assert.Contains("login.microsoftonline.com", location);
        Assert.Contains("Calendars.Read", System.Net.WebUtility.UrlDecode(location));
        Assert.Contains("offline_access", System.Net.WebUtility.UrlDecode(location));
        Assert.Contains("signin-connect-entra", System.Net.WebUtility.UrlDecode(location));
        Assert.Contains("prompt=select_account", location);
    }

    [Fact]
    public async Task Disconnect_RemovesConnector_AndUpdatesCard()
    {
        await using var factory = new ApskaitaApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        var userId = ConnectorUserId(factory);
        Guid connectorId;
        await using (var db = factory.NewDbContext())
        {
            var connector = NewConnector(userId, "outlook-user@contoso.com", "Outlook User");
            connectorId = connector.Id;
            db.CalendarConnectors.Add(connector);
            await db.SaveChangesAsync(Ct);
        }

        var http = factory.CreateClient();
        var listPage = await http.GetStringAsync("/account/me");
        var token = Regex.Match(listPage, "request-verification-token\"\\s+content=\"([^\"]+)\"").Groups[1].Value;
        Assert.True(!string.IsNullOrEmpty(token), "Antiforgery token should be present.");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/account/disconnect/{connectorId}");
        request.Headers.TryAddWithoutValidation("HX-Request", "true");
        request.Headers.TryAddWithoutValidation("RequestVerificationToken", token);

        var response = await http.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}.");

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("connector-card-outlook365", content);
        Assert.Contains("No accounts connected yet.", content);

        await using (var db = factory.NewDbContext())
        {
            Assert.False(db.CalendarConnectors.Any(), "Connector row should be deleted.");
        }
    }

    [Fact]
    public async Task TimeTrackingList_RendersStubbedCalendarEvents()
    {
        await using var factory = new CalendarStubApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        factory.Reader.Result = new CalendarReadResult(true, [
            new CalendarEventInfo(
                "Sprint planning",
                new DateTime(2026, 10, 12, 9, 0, 0),
                new DateTime(2026, 10, 12, 9, 30, 0),
                "Meeting Room 1",
                false,
                "Organizer Person")
        ], null);

        var html = await factory.CreateClient().GetStringAsync("/time-tracking/list");

        Assert.Contains("Calendar", html);
        Assert.Contains("Sprint planning", html);
        Assert.Contains("09:00 - 09:30", html);
        Assert.Contains("Meeting Room 1", html);
    }

    [Fact]
    public async Task TimeTrackingList_ShowsConnectHint_WhenNoConnector()
    {
        await using var factory = new CalendarStubApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        var html = await factory.CreateClient().GetStringAsync("/time-tracking/list");

        Assert.Contains("No calendar connected.", html);
        Assert.Contains("/account", html);
    }

    [Fact]
    public async Task TimeTrackingList_ShowsNotice_WhenSyncFailed()
    {
        await using var factory = new CalendarStubApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        factory.Reader.Result = new CalendarReadResult(true, [], "Calendar unavailable: Graph 500");

        var response = await factory.CreateClient().GetAsync("/time-tracking/list");
        Assert.True(response.IsSuccessStatusCode, $"Expected 200, got {(int)response.StatusCode}.");

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Calendar unavailable: Graph 500", html);
    }

    [Fact]
    public async Task TimeTrackingList_CalendarEvents_AreDisplayOnly()
    {
        await using var factory = new CalendarStubApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        factory.Reader.Result = new CalendarReadResult(true, [
            new CalendarEventInfo(
                "Standup",
                new DateTime(2026, 10, 12, 8, 0, 0),
                new DateTime(2026, 10, 12, 8, 15, 0),
                null,
                true,
                null)
        ], null);

        var html = await factory.CreateClient().GetStringAsync("/time-tracking/list");

        Assert.Contains("Standup", html);
        Assert.Contains("Online", html);

        // REQ-008: no htmx wiring on calendar event elements.
        var panelStart = html.IndexOf("tt-calendar-panel", StringComparison.Ordinal);
        Assert.True(panelStart >= 0, "Calendar panel should be rendered.");
        var panel = html.Substring(panelStart);
        Assert.DoesNotContain("hx-get", panel);
        Assert.DoesNotContain("hx-post", panel);
    }
}
