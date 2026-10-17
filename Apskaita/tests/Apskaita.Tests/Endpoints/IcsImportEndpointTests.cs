using System.Text;
using Apskaita.Entities;

namespace Apskaita.Tests.Endpoints;

/// <summary>
/// Integration tests for the imported (.ics) calendar flow on the account page: a user creates a NAMED
/// imported calendar, uploads an <c>.ics</c> file into it, and the calendar's events appear in the
/// connections table (with statistics) and in the time tracking panel. An import replaces only the events —
/// the connector row survives — and the raw file is never persisted (SEC-005).
/// </summary>
[Collection("Integration")] // in-process hosts must not run concurrently (ApskaitaApiFactory race)
public sealed class IcsImportEndpointTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private const string UserEmail = "ics@example.com";

    private static async Task SeedAsync(ApskaitaApiFactory factory)
    {
        var userId = Guid.NewGuid();
        await using (var db = factory.NewDbContext())
        {
            db.Users.Add(new User
            {
                Id = userId,
                Email = UserEmail,
                DisplayName = "ICS User",
                Provider = "entra",
                SubjectId = "ics-subject",
                IsActive = true,
                Role = UserRole.User
            });
            await db.SaveChangesAsync(Ct);
        }

        factory.SetPrincipal(UserEmail, UserRole.User, userId);
    }

    private static async Task PostFormAsync(HttpClient http, string url, IDictionary<string, string> fields)
    {
        var response = await PostFormRawAsync(http, url, fields);
        Assert.True(response.IsSuccessStatusCode, $"Expected success for {url}, got {(int)response.StatusCode}.");
    }

    private static async Task<HttpResponseMessage> PostFormRawAsync(HttpClient http, string url, IDictionary<string, string> fields)
    {
        var token = await TestAntiforgery.GetTokenAsync(http, "/account/me");
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(fields)
        };
        AddHtmxHeaders(request, token);
        return await http.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PostFileAsync(HttpClient http, string url, string? fileName, string content)
    {
        var token = await TestAntiforgery.GetTokenAsync(http, "/account/me");
        var form = new MultipartFormDataContent();
        if (fileName is not null)
        {
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
            file.Headers.TryAddWithoutValidation("Content-Type", "text/calendar");
            form.Add(file, "file", fileName);
        }
        else
        {
            // A multipart body with no file part — what a browser sends when the input is left empty.
            form.Add(new StringContent("none"), "note");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
        AddHtmxHeaders(request, token);
        return await http.SendAsync(request);
    }

    private static void AddHtmxHeaders(HttpRequestMessage request, string token)
    {
        request.Headers.TryAddWithoutValidation("RequestVerificationToken", token);
        request.Headers.TryAddWithoutValidation("HX-Request", "true");
        request.Headers.TryAddWithoutValidation("HX-Target", "connectors-table");
    }

    private static string Toasts(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("HX-Trigger", out var values), "Expected an HX-Trigger header.");
        return string.Join(",", values);
    }

    /// <summary>Monday of the current UTC week — the week the grid is asked to render.</summary>
    private static DateTime WeekMonday() => DateTime.UtcNow.Date.AddDays(-(((int)DateTime.UtcNow.DayOfWeek + 6) % 7));

    private static string Stamp(DateTime value) => value.ToString("yyyyMMdd") + "T090000Z";

    private static string BuildIcs(params (string Uid, DateTime Start, string Subject)[] events)
    {
        var lines = new List<string> { "BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//Apskaita//Tests//EN" };

        foreach (var evt in events)
        {
            lines.AddRange([
                "BEGIN:VEVENT",
                $"UID:{evt.Uid}",
                $"DTSTART:{Stamp(evt.Start)}",
                $"DTEND:{Stamp(evt.Start.AddMinutes(30))}",
                $"SUMMARY:{evt.Subject}",
                "END:VEVENT"]);
        }

        lines.Add("END:VCALENDAR");
        return string.Join("\r\n", lines) + "\r\n";
    }

    /// <summary>A minimal two-event calendar whose events land in the current week.</summary>
    private static string SampleIcs(string firstSubject = "Imported sync")
    {
        var monday = WeekMonday();
        return BuildIcs(
            ("imported-1", monday.AddDays(1), firstSubject),
            ("imported-2", monday.AddDays(3), "Imported review"));
    }

    private static string SingleEventIcs(string subject) =>
        BuildIcs(("imported-only", WeekMonday().AddDays(1), subject));

    /// <summary>One series expanded to three occurrences, all inside the displayed week.</summary>
    private static string RecurringIcs()
    {
        var monday = WeekMonday();
        var lines = new[]
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//Apskaita//Tests//EN",
            "BEGIN:VEVENT",
            "UID:standup",
            $"DTSTART:{StampAt(monday, "090000")}",
            $"DTEND:{StampAt(monday, "093000")}",
            "SUMMARY:Standup",
            "RRULE:FREQ=DAILY;COUNT=3",
            "END:VEVENT",
            "END:VCALENDAR"
        };

        return string.Join("\r\n", lines) + "\r\n";
    }

    private static string StampAt(DateTime value, string time) => value.ToString("yyyyMMdd") + "T" + time + "Z";

    private static async Task<Guid> CreateCalendarAsync(ApskaitaApiFactory factory, HttpClient http, string name)
    {
        await PostFormAsync(http, "/account/ics/create", new Dictionary<string, string> { ["name"] = name });

        await using var db = factory.NewDbContext();
        return db.CalendarConnectors.Single(c => c.Provider == CalendarProvider.Ics).Id;
    }

    [Fact]
    public async Task AccountPage_ShowsTheImportedCalendarProvider_WithACreateForm()
    {
        await using var factory = new ApskaitaApiFactory();
        await SeedAsync(factory);
        var http = factory.CreateClient();

        var html = await http.GetStringAsync("/account/me");

        Assert.Contains("Imported calendar (.ics)", html);
        Assert.Contains("/account/ics/create", html);
        Assert.Contains("Create calendar", html);
        Assert.Contains("No imported calendars yet.", html);

        // The ICS provider has no OAuth consent link, unlike Outlook 365.
        Assert.Contains("/account/connect/outlook365", html);
        Assert.DoesNotContain("/account/connect/ics", html);
    }

    [Fact]
    public async Task Create_StoresTheEnteredName_AsTheAccountKey()
    {
        await using var factory = new ApskaitaApiFactory();
        await SeedAsync(factory);
        var http = factory.CreateClient();

        await PostFormAsync(http, "/account/ics/create", new Dictionary<string, string> { ["name"] = "NrdCompanies" });

        await using var db = factory.NewDbContext();
        var connector = db.CalendarConnectors.Single(c => c.Provider == CalendarProvider.Ics);

        Assert.Equal("nrdcompanies", connector.AccountKey);
        Assert.Equal("NrdCompanies", connector.DisplayName);
        Assert.Equal("NrdCompanies", connector.AccountEmail);
        Assert.Equal(string.Empty, connector.RefreshTokenProtected);
    }

    [Fact]
    public async Task Create_DuplicateName_ReportsTheReason_AndStoresNothing()
    {
        await using var factory = new ApskaitaApiFactory();
        await SeedAsync(factory);
        var http = factory.CreateClient();

        await PostFormAsync(http, "/account/ics/create", new Dictionary<string, string> { ["name"] = "nrdcompanies" });
        var response = await PostFormRawAsync(http, "/account/ics/create", new Dictionary<string, string> { ["name"] = "NrdCompanies" });

        Assert.True(response.IsSuccessStatusCode, $"Expected 200 with a toast, got {(int)response.StatusCode}.");
        Assert.Contains("already exists", Toasts(response), StringComparison.OrdinalIgnoreCase);

        await using var db = factory.NewDbContext();
        Assert.Single(db.CalendarConnectors.Where(c => c.Provider == CalendarProvider.Ics));
    }

    [Fact]
    public async Task Create_ThenImport_KeepsTheConnector_AndStoresItsEvents()
    {
        await using var factory = new ApskaitaApiFactory();
        await SeedAsync(factory);
        var http = factory.CreateClient();
        var connectorId = await CreateCalendarAsync(factory, http, "nrdcompanies");

        var response = await PostFileAsync(http, $"/account/ics/{connectorId}/import", "calendar.ics", SampleIcs());

        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}.");
        Assert.Contains("event(s) imported", Toasts(response), StringComparison.OrdinalIgnoreCase);

        await using var db = factory.NewDbContext();
        Assert.Equal(2, db.CalendarImportedEvents.Count());
        Assert.All(db.CalendarImportedEvents.ToArray(), e => Assert.Equal(connectorId, e.ConnectorId));
        Assert.NotNull(db.CalendarConnectors.Single(c => c.Id == connectorId).LastSyncAtUtc);
    }

    [Fact]
    public async Task ReImport_ReplacesTheEvents_WithoutTouchingTheConnector()
    {
        await using var factory = new ApskaitaApiFactory();
        await SeedAsync(factory);
        var http = factory.CreateClient();
        var connectorId = await CreateCalendarAsync(factory, http, "nrdcompanies");

        await PostFileAsync(http, $"/account/ics/{connectorId}/import", "first.ics", SampleIcs("First subject"));

        // A second upload with one event: the snapshot changes, the calendar itself does not.
        await PostFileAsync(http, $"/account/ics/{connectorId}/import", "second.ics", SingleEventIcs("Second subject"));

        await using var db = factory.NewDbContext();
        var rows = db.CalendarImportedEvents.ToArray();

        Assert.Single(rows);
        Assert.Equal("Second subject", rows[0].Subject);
        Assert.Equal(1, db.CalendarConnectors.Count(c => c.Provider == CalendarProvider.Ics));
        Assert.Equal(connectorId, db.CalendarConnectors.Single(c => c.Provider == CalendarProvider.Ics).Id);
    }

    [Fact]
    public async Task Import_NonCalendarFile_StoresNothing_AndNamesTheReason()
    {
        await using var factory = new ApskaitaApiFactory();
        await SeedAsync(factory);
        var http = factory.CreateClient();
        var connectorId = await CreateCalendarAsync(factory, http, "nrdcompanies");

        var response = await PostFileAsync(http, $"/account/ics/{connectorId}/import", "notes.txt", "hello, not a calendar");

        Assert.True(response.IsSuccessStatusCode, $"Expected 200 with a toast, got {(int)response.StatusCode}.");
        Assert.Contains(".ics", Toasts(response), StringComparison.OrdinalIgnoreCase);

        await using var db = factory.NewDbContext();
        Assert.Empty(db.CalendarImportedEvents);
    }

    [Fact]
    public async Task Import_WithoutAFile_ReportsItAndStoresNothing()
    {
        await using var factory = new ApskaitaApiFactory();
        await SeedAsync(factory);
        var http = factory.CreateClient();
        var connectorId = await CreateCalendarAsync(factory, http, "nrdcompanies");

        var response = await PostFileAsync(http, $"/account/ics/{connectorId}/import", fileName: null, content: string.Empty);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("Choose an .ics file", Toasts(response), StringComparison.OrdinalIgnoreCase);

        await using var db = factory.NewDbContext();
        Assert.Empty(db.CalendarImportedEvents);
    }

    [Fact]
    public async Task Import_AnotherUsersCalendar_ReportsAnError_AndStoresNothing()
    {
        await using var factory = new ApskaitaApiFactory();
        await SeedAsync(factory);
        var http = factory.CreateClient();
        var connectorId = await CreateCalendarAsync(factory, http, "nrdcompanies");

        // A different session user must not reach someone else's calendar: the endpoint answers 404, which
        // the HTMX error middleware turns into an error toast for the browser.
        factory.SetPrincipal("intruder@example.com", UserRole.User, Guid.NewGuid());
        var response = await PostFileAsync(http, $"/account/ics/{connectorId}/import", "calendar.ics", SampleIcs());

        Assert.Contains("not found", Toasts(response), StringComparison.OrdinalIgnoreCase);

        await using var db = factory.NewDbContext();
        Assert.Empty(db.CalendarImportedEvents);
    }

    [Fact]
    public async Task ConnectorsTable_ShowsEventCountAndCoveredRange()
    {
        await using var factory = new ApskaitaApiFactory();
        await SeedAsync(factory);
        var http = factory.CreateClient();
        var connectorId = await CreateCalendarAsync(factory, http, "nrdcompanies");
        await PostFileAsync(http, $"/account/ics/{connectorId}/import", "calendar.ics", SampleIcs());

        var html = await http.GetStringAsync("/account/me");

        Assert.Contains("2 event(s)", html);
        Assert.Contains("from ", html);
        Assert.Contains("connector-ics-row", html);
    }

    [Fact]
    public async Task Disconnect_RemovesTheCalendarAndItsEvents()
    {
        await using var factory = new ApskaitaApiFactory();
        await SeedAsync(factory);
        var http = factory.CreateClient();
        var connectorId = await CreateCalendarAsync(factory, http, "nrdcompanies");
        await PostFileAsync(http, $"/account/ics/{connectorId}/import", "calendar.ics", SampleIcs());

        var token = await TestAntiforgery.GetTokenAsync(http, "/account/me");
        var request = new HttpRequestMessage(HttpMethod.Post, $"/account/disconnect/{connectorId}");
        AddHtmxHeaders(request, token);
        var response = await http.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}.");

        await using var db = factory.NewDbContext();
        Assert.Empty(db.CalendarConnectors.Where(c => c.Provider == CalendarProvider.Ics));

        // The panel no longer reports a connection, so the imported events are gone from the week view.
        var grid = await TestHtmx.GetHtmlAsync(http, "/time-tracking/list?weekStart=" + WeekStart(), "time-tracking-grid");
        Assert.DoesNotContain("Imported sync", grid);
    }

    [Fact]
    public async Task TimeTrackingGrid_ShowsImportedEvents_WithTheIcsEventLink()
    {
        await using var factory = new ApskaitaApiFactory();
        await SeedAsync(factory);
        var http = factory.CreateClient();
        var connectorId = await CreateCalendarAsync(factory, http, "nrdcompanies");
        await PostFileAsync(http, $"/account/ics/{connectorId}/import", "calendar.ics", SampleIcs());

        var grid = await TestHtmx.GetHtmlAsync(http, "/time-tracking/list?weekStart=" + WeekStart(), "time-tracking-grid");

        Assert.Contains("Imported sync", grid);
        Assert.Contains("provider=ics", grid);
        Assert.Contains("account=nrdcompanies", grid);
        Assert.Contains("eventId=imported-1", grid);
    }

    [Fact]
    public async Task TimeTrackingGrid_LinksEveryOccurrenceOfARecurringSeries_Separately()
    {
        await using var factory = new ApskaitaApiFactory();
        await SeedAsync(factory);
        var http = factory.CreateClient();
        var connectorId = await CreateCalendarAsync(factory, http, "nrdcompanies");
        await PostFileAsync(http, $"/account/ics/{connectorId}/import", "series.ics", RecurringIcs());

        var grid = await TestHtmx.GetHtmlAsync(http, "/time-tracking/list?weekStart=" + WeekStart(), "time-tracking-grid");

        // Three occurrences of one series, three distinct links: logging one must not mark the others as
        // logged, because the identity triple is provider + account + event id.
        var ids = grid
            .Split("eventId=")
            .Skip(1)
            .Select(part => Uri.UnescapeDataString(part.Split('&', '"')[0]))
            .Distinct()
            .ToArray();

        Assert.Equal(3, ids.Length);
        Assert.All(ids, id => Assert.StartsWith("standup@", id));

        await using var db = factory.NewDbContext();
        Assert.All(db.CalendarImportedEvents.ToArray(), e => Assert.Equal("standup", e.GroupId));
    }

    [Fact]
    public async Task ImportedText_IsHtmlEncodedInThePanel()
    {
        await using var factory = new ApskaitaApiFactory();
        await SeedAsync(factory);
        var http = factory.CreateClient();
        var connectorId = await CreateCalendarAsync(factory, http, "nrdcompanies");
        await PostFileAsync(http, $"/account/ics/{connectorId}/import", "calendar.ics", SampleIcs("<script>alert(1)</script>"));

        var grid = await TestHtmx.GetHtmlAsync(http, "/time-tracking/list?weekStart=" + WeekStart(), "time-tracking-grid");

        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", grid);
        Assert.DoesNotContain("<script>alert(1)</script>", grid);
    }

    private static string WeekStart() => WeekMonday().ToString("yyyy-MM-dd");
}
