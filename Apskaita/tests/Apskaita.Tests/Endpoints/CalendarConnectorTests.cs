using System.Net;
using System.Text.RegularExpressions;
using Apskaita.Entities;
using Apskaita.Shared.Calendar;
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
[Collection("Integration")] // in-process hosts must not run concurrently (ApskaitaApiFactory race)
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
    public async Task TimeTrackingRoot_RendersListPageWithGridPlaceholder()
    {
        await using var factory = new ApskaitaApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        var html = await factory.CreateClient().GetStringAsync("/time-tracking");

        // The page renders controls plus an empty placeholder; the grid arrives from its own hx-get.
        Assert.Contains("hx-get=\"/time-tracking/list?weekStart=", html);
        Assert.Contains("hx-trigger=\"load, time-tracking-list-refresh from:document\"", html);
        Assert.Contains("Loading week", html);
        Assert.DoesNotContain("tt-week-grid", html);
        Assert.DoesNotContain("tt-calendar-panel", html);

        // The connections dock is a sibling fragment with its own load-only trigger, so a grid
        // refresh never re-fetches it. It carries the displayed week because an imported calendar's
        // health dot is week-relative.
        Assert.Contains("hx-get=\"/account/connections?weekStart=", html);
        Assert.Contains("hx-trigger=\"load\"", html);
    }

    [Fact]
    public async Task TimeTrackingList_NonHtmx_RedirectsToRoot()
    {
        await using var factory = new ApskaitaApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/time-tracking/list?weekStart=2026-10-05");

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, $"Expected 302, got {(int)response.StatusCode}.");
        Assert.Equal("/time-tracking?weekStart=2026-10-05", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task TimeTrackingGridFragment_CarriesRefreshTriggerForItsOwnWeek()
    {
        await using var factory = new CalendarStubApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        var html = await TestHtmx.GetHtmlAsync(factory.CreateClient(), "/time-tracking/list?weekStart=2026-10-05", "time-tracking-grid");

        // The outerHTML swap replaces the placeholder, so the incoming grid must own the refresh wiring
        // for its own week; "load" stays on the page placeholder (otherwise it would re-fire forever).
        Assert.Contains("id=\"time-tracking-grid\"", html);
        Assert.Contains("hx-get=\"/time-tracking/list?weekStart=2026-10-05\"", html);
        Assert.Contains("hx-trigger=\"time-tracking-list-refresh from:document\"", html);
        Assert.Contains("data-move-url=\"/time-tracking/move\"", html);
        Assert.DoesNotContain("load,", html);
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
        Assert.Contains("Calendars.Read", WebUtility.UrlDecode(location));
        Assert.Contains("offline_access", WebUtility.UrlDecode(location));
        Assert.Contains("signin-connect-entra", WebUtility.UrlDecode(location));
        Assert.Contains("prompt=select_account", location);
        Assert.DoesNotContain("response_mode=form_post", location);
    }

    [Fact]
    public async Task Disconnect_RemovesConnector_AndUpdatesTable()
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
        Assert.Contains("connectors-table", content);
        Assert.Contains("No accounts connected yet.", content);

        await using (var db = factory.NewDbContext())
        {
            Assert.False(db.CalendarConnectors.Any(), "Connector row should be deleted.");
        }
    }

    [Fact]
    public async Task TimeTrackingList_RendersStubbedCalendarEventsInGrid()
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

        var html = await TestHtmx.GetHtmlAsync(factory.CreateClient(), "/time-tracking/list", "time-tracking-grid");

        // Events render inline in the grid as grey cards on the day/time layout (no side panel).
        Assert.Contains("tt-event-card", html);
        Assert.Contains("Sprint planning", html);
        Assert.Contains("09:00 - 09:30", html);
        Assert.DoesNotContain("tt-calendar-panel", html);
    }

    [Fact]
    public async Task TimeTrackingList_RendersAllDayEventInAllDayRow()
    {
        await using var factory = new CalendarStubApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        // Midnight-to-midnight = an all-day event: it belongs in the dedicated all-day row, not the
        // timed slot grid.
        factory.Reader.Result = new CalendarReadResult(true, [
            new CalendarEventInfo(
                "Public holiday",
                new DateTime(2026, 10, 12),
                new DateTime(2026, 10, 13),
                null,
                false,
                null)
        ], null);

        var html = await TestHtmx.GetHtmlAsync(factory.CreateClient(), "/time-tracking/list", "time-tracking-grid");

        Assert.Contains("tt-grid-allday-cell", html);
        Assert.Contains("tt-allday-band", html);
        Assert.Contains("Public holiday", html);
        Assert.DoesNotContain("tt-event-card", html);
    }

    [Fact]
    public async Task ConnectionsDock_ListsConnectorsWithoutEventsOrStats()
    {
        await using var factory = new CalendarStubApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        var userId = ConnectorUserId(factory);
        await using (var db = factory.NewDbContext())
        {
            db.CalendarConnectors.Add(NewConnector(userId, "outlook-user@contoso.com", "Outlook User"));
            await db.SaveChangesAsync(Ct);
        }

        // The dock shows connection status only — even when the reader has events for the week.
        factory.Reader.Result = new CalendarReadResult(true, [
            new CalendarEventInfo(
                "Sprint planning",
                new DateTime(2026, 10, 12, 9, 0, 0),
                new DateTime(2026, 10, 12, 9, 30, 0),
                null,
                false,
                null)
        ], null);

        var html = await TestHtmx.GetHtmlAsync(factory.CreateClient(), "/account/connections?weekStart=2026-10-12", "tt-connections-dock");

        Assert.Contains("Connections", html);
        Assert.Contains("Outlook User", html);
        Assert.Contains("tt-connections-dot is-ok", html);
        Assert.Contains("/account/connect/outlook365", html);
        Assert.Contains("/account/me", html);

        // Managing (disconnect, calendar creation) happens on the account page, not in the dock.
        Assert.DoesNotContain("disconnect/", html);

        // No events, and no statistics (those stay on /account/me).
        Assert.DoesNotContain("Sprint planning", html);
        Assert.DoesNotContain("event(s)", html);
        Assert.DoesNotContain("connectors-table", html);
    }

    [Fact]
    public async Task ConnectionsDock_WithoutConnector_ShowsConnectAction()
    {
        await using var factory = new CalendarStubApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        var html = await TestHtmx.GetHtmlAsync(factory.CreateClient(), "/account/connections?weekStart=2026-10-12", "tt-connections-dock");

        Assert.Contains("Connect account", html);
        Assert.Contains("/account/connect/outlook365", html);
        Assert.Contains("None yet.", html);
    }

    [Fact]
    public async Task ConnectionsDock_WithSyncError_ShowsRedDotWithReason()
    {
        await using var factory = new CalendarStubApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        var userId = ConnectorUserId(factory);
        await using (var db = factory.NewDbContext())
        {
            var connector = NewConnector(userId, "outlook-user@contoso.com", "Outlook User");
            connector.LastSyncError = "Calendar unavailable: Graph 500";
            db.CalendarConnectors.Add(connector);
            await db.SaveChangesAsync(Ct);
        }

        var html = await TestHtmx.GetHtmlAsync(factory.CreateClient(), "/account/connections?weekStart=2026-10-12", "tt-connections-dock");

        Assert.Contains("tt-connections-dot is-bad", html);
        Assert.Contains("Calendar unavailable: Graph 500", html);
    }

    [Fact]
    public async Task ConnectionsDock_ImportedCalendarCoveringWeek_ShowsGreenDot()
    {
        await using var factory = new CalendarStubApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        var userId = ConnectorUserId(factory);
        Guid connectorId;
        await using (var db = factory.NewDbContext())
        {
            var connector = NewConnector(userId, "nrd", "nrd");
            connector.Provider = CalendarProvider.Ics;
            connectorId = connector.Id;
            db.CalendarConnectors.Add(connector);
            db.CalendarImportedEvents.AddRange(
                new CalendarImportedEvent { ConnectorId = connectorId, ExternalEventId = "e1", GroupId = null, StartUtc = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc), EndUtc = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc), Subject = "Old", CreatedAtUtc = DateTime.UtcNow },
                new CalendarImportedEvent { ConnectorId = connectorId, ExternalEventId = "e2", GroupId = null, StartUtc = new DateTime(2026, 10, 13, 9, 0, 0, DateTimeKind.Utc), EndUtc = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc), Subject = "In week", CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync(Ct);
        }

        var html = await TestHtmx.GetHtmlAsync(factory.CreateClient(), "/account/connections?weekStart=2026-10-12", "tt-connections-dock");

        Assert.Contains("tt-connections-dot is-ok", html);
        // The row links to the import dialog instead of carrying an inline file picker.
        Assert.Contains($"/account/ics/{connectorId}/import-dialog", html);
        Assert.DoesNotContain("type=\"file\"", html);
    }

    [Fact]
    public async Task ConnectionsDock_ImportedCalendarOutsideWeek_ShowsRedDot()
    {
        await using var factory = new CalendarStubApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        var userId = ConnectorUserId(factory);
        Guid connectorId;
        await using (var db = factory.NewDbContext())
        {
            var connector = NewConnector(userId, "nrd", "nrd");
            connector.Provider = CalendarProvider.Ics;
            connectorId = connector.Id;
            db.CalendarConnectors.Add(connector);
            db.CalendarImportedEvents.Add(new CalendarImportedEvent
            {
                ConnectorId = connectorId,
                ExternalEventId = "e1",
                GroupId = null,
                StartUtc = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
                EndUtc = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
                Subject = "Long ago",
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync(Ct);
        }

        // Displayed week is in 2027 while the calendar only covers January 2026.
        var html = await TestHtmx.GetHtmlAsync(factory.CreateClient(), "/account/connections?weekStart=2027-01-11", "tt-connections-dock");

        Assert.Contains("tt-connections-dot is-bad", html);
        Assert.Contains("nothing in this week", html);
    }

    [Fact]
    public async Task IcsImportDialog_RendersFilePickerPostingToImport()
    {
        await using var factory = new CalendarStubApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        var userId = ConnectorUserId(factory);
        Guid connectorId;
        await using (var db = factory.NewDbContext())
        {
            var connector = NewConnector(userId, "nrd", "nrd");
            connector.Provider = CalendarProvider.Ics;
            connectorId = connector.Id;
            db.CalendarConnectors.Add(connector);
            await db.SaveChangesAsync(Ct);
        }

        var html = await TestHtmx.GetHtmlAsync(factory.CreateClient(), $"/account/ics/{connectorId}/import-dialog", "dialog-host");

        Assert.Contains("Import into", html);
        Assert.Contains("type=\"file\"", html);
        Assert.Contains($"/account/ics/{connectorId}/import", html);
        Assert.Contains("name=\"surface\"", html);
    }

    [Fact]
    public async Task ConnectionsDock_RefreshesItselfNotTheTable()
    {
        await using var factory = new CalendarStubApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        var html = await TestHtmx.GetHtmlAsync(factory.CreateClient(), "/account/connections?weekStart=2026-10-12", "tt-connections-dock");

        // The dock re-fetches on its own trigger; the account page's table OOB is not emitted here.
        Assert.Contains("hx-trigger=\"connections-refresh from:document\"", html);

        // Its refresh URL echoes the requested week (the display-zone date, never the UTC-shifted one).
        Assert.Contains("hx-get=\"/account/connections?weekStart=2026-10-12\"", html);
    }

    [Fact]
    public async Task TimeTrackingGridFragment_DoesNotCarryTheDock()
    {
        await using var factory = new CalendarStubApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        var html = await TestHtmx.GetHtmlAsync(factory.CreateClient(), "/time-tracking/list", "time-tracking-grid");

        // The dock lives in the page shell, outside #time-tracking-grid, so a grid refresh never
        // re-fetches it.
        Assert.DoesNotContain("tt-connections-dock", html);
        Assert.DoesNotContain("/account/connections", html);
    }

    [Fact]
    public async Task TimeTrackingList_ShowsNotice_WhenSyncFailed()
    {
        await using var factory = new CalendarStubApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        factory.Reader.Result = new CalendarReadResult(true, [], "Calendar unavailable: Graph 500");

        var html = await TestHtmx.GetHtmlAsync(factory.CreateClient(), "/time-tracking/list", "time-tracking-grid");

        Assert.Contains("Calendar unavailable: Graph 500", html);
    }

    [Fact]
    public async Task TimeTrackingList_IdentitylessCalendarEvents_AreInert()
    {
        await using var factory = new CalendarStubApiFactory();
        factory.SetPrincipal(UserEmail, UserRole.User);
        await SeedUserAsync(factory);

        // Events without an identity triple (no provider/event id) cannot be referenced -> no click wiring.
        factory.Reader.Result = new CalendarReadResult(true, [
            new CalendarEventInfo(
                "Standup",
                new DateTime(2026, 10, 12, 8, 0, 0),
                new DateTime(2026, 10, 12, 8, 15, 0),
                null,
                true,
                null)
        ], null);

        var html = await TestHtmx.GetHtmlAsync(factory.CreateClient(), "/time-tracking/list", "time-tracking-grid");

        Assert.Contains("Standup", html);

        // Events without an identity triple still render (display-only) but carry no click wiring.
        var cardStart = html.IndexOf("tt-event-card", StringComparison.Ordinal);
        Assert.True(cardStart >= 0, "The event card should render in the grid.");
        var cardEnd = html.IndexOf("</div>", cardStart, StringComparison.Ordinal);
        var card = html[cardStart..cardEnd];
        Assert.DoesNotContain("hx-get", card);
        Assert.DoesNotContain("hx-post", card);
    }
}
