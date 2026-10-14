using System.Net;
using Apskaita.Entities;
using Apskaita.Shared.Calendar;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Tests.Endpoints;

/// <summary>
/// feature-time-tracking-from-calendar-1: the single <c>GET event</c> URI (server decides create vs
/// edit dialog), reference validation, and reference persistence/dedupe on create.
/// </summary>
[Collection("Integration")] // in-process hosts must not run concurrently (ApskaitaApiFactory race)
public sealed class TimeTrackingFromCalendarTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private const string UserEmail = "connector@example.com";
    private const string Account = "a@contoso.com";
    private const string EventId = "evt-abc-1";

    private static async Task<(ApskaitaApiFactory factory, Guid projectId)> SetupAsync(CalendarStubApiFactory factory)
    {
        Guid userId;
        var projectId = Guid.NewGuid();

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
            db.Projects.Add(new Project
            {
                Id = projectId,
                ClientId = Guid.NewGuid(),
                InvoiceTemplateId = Guid.NewGuid(),
                Name = "Karma",
                ColorRgb = "#F4B6C2",
                CurrencyCode = "EUR",
                IsActive = true
            });
            db.CalendarConnectors.Add(new CalendarConnector
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Provider = CalendarProvider.Outlook365,
                AccountKey = Account,
                AccountEmail = Account,
                DisplayName = "Work",
                RefreshTokenProtected = "protected-token",
                Scopes = "https://graph.microsoft.com/Calendars.Read offline_access",
                ConnectedAtUtc = new DateTime(2026, 10, 11, 10, 0, 0, DateTimeKind.Utc),
                CreatedAtUtc = new DateTime(2026, 10, 11, 10, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2026, 10, 11, 10, 0, 0, DateTimeKind.Utc)
            });
            await db.SaveChangesAsync(Ct);
        }

        factory.SetPrincipal(UserEmail, UserRole.User, userId);
        return (factory, projectId);
    }

    private static string EventUrl(string query = "") =>
        $"/time-tracking/event?date=2026-10-13&time=09:45&duration=45&weekStart=2026-10-12&provider=outlook365&account={Account}&eventId={EventId}{query}";

    private static CalendarEventInfo Standup() => new(
        "FDMS standup",
        new DateTime(2026, 10, 13, 9, 45, 0),
        new DateTime(2026, 10, 13, 10, 30, 0),
        null,
        false,
        null,
        "outlook365",
        Account,
        EventId);

    [Fact]
    public async Task Event_Untracked_ReturnsPrefilledCreateDialog_WithHiddenReference()
    {
        await using var factory = new CalendarStubApiFactory();
        await SetupAsync(factory);

        var html = await factory.CreateClient().GetStringAsync(EventUrl());

        Assert.Contains("Create time entry", html);
        Assert.Contains("name=\"EventId\"", html);
        Assert.Contains(EventId, html);
        Assert.Contains("value=\"45\"", html);
        // REQ-008: recurrence is suppressed for event-derived entries.
        Assert.DoesNotContain("tt-repeat-toggle", html);
    }

    [Fact]
    public async Task Event_Tracked_ReturnsEditDialogForExistingEntry()
    {
        await using var factory = new CalendarStubApiFactory();
        var (seeded, projectId) = await SetupAsync(factory);

        await using (var db = seeded.NewDbContext())
        {
            db.TimeTrackings.Add(new TimeTracking
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                StartedAtUtc = new DateTime(2026, 10, 13, 6, 45, 0, DateTimeKind.Utc),
                DurationInMinutes = 45,
                Description = "standup logged earlier",
                CalendarEvent = new CalendarEventReference(CalendarProvider.Outlook365, Account, EventId),
                CreatedAtUtc = new DateTime(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc)
            });
            await db.SaveChangesAsync(Ct);
        }

        var html = await seeded.CreateClient().GetStringAsync(EventUrl());

        Assert.Contains("Edit time entry", html);
        Assert.Contains("standup logged earlier", html);
        Assert.DoesNotContain("name=\"EventId\"", html);
    }

    [Fact]
    public async Task Event_ForgedAccount_Returns404()
    {
        await using var factory = new CalendarStubApiFactory();
        await SetupAsync(factory);

        var response = await factory.CreateClient().GetAsync(EventUrl("&account=intruder@evil.com").Replace($"&account={Account}", string.Empty));

        Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"Expected 404 for a forged account, got {(int)response.StatusCode}.");
    }

    [Fact]
    public async Task Event_UnknownProvider_Returns404()
    {
        await using var factory = new CalendarStubApiFactory();
        await SetupAsync(factory);

        var url = EventUrl()
            .Replace($"&account={Account}", $"&account={Account}&bogus=1")
            .Replace("provider=outlook365", "provider=notreal");
        var response = await factory.CreateClient().GetAsync(url);

        Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"Expected 404 for an unknown provider, got {(int)response.StatusCode}.");
    }

    private static Dictionary<string, string> CreateForm(Guid projectId) => new()
    {
        ["Date"] = "2026-10-13",
        ["Start"] = "09:45",
        ["DurationMinutes"] = "45",
        ["Description"] = "standup",
        ["ProjectId"] = projectId.ToString(),
        ["ProjectLabel"] = "Karma"
    };

    private static async Task<HttpResponseMessage> PostCreateAsync(ApskaitaApiFactory factory, Dictionary<string, string> form)
    {
        var http = factory.CreateClient();
        var token = await TestAntiforgery.GetTokenAsync(http, "/time-tracking/list");
        var request = new HttpRequestMessage(HttpMethod.Post, "/time-tracking/create")
        {
            Content = new FormUrlEncodedContent(form)
        };
        request.Headers.TryAddWithoutValidation("RequestVerificationToken", token);
        request.Headers.TryAddWithoutValidation("HX-Request", "true");
        request.Headers.TryAddWithoutValidation("HX-Target", "dialog-host");
        return await http.SendAsync(request);
    }

    [Fact]
    public async Task Create_WithEventReference_StoresOwnedReference()
    {
        await using var factory = new CalendarStubApiFactory();
        var (seeded, projectId) = await SetupAsync(factory);

        var form = CreateForm(projectId);
        form["EventProviderKey"] = "outlook365";
        form["EventAccountKey"] = Account;
        form["EventId"] = EventId;

        var response = await PostCreateAsync(seeded, form);
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}.");

        await using var db = seeded.NewDbContext();
        var row = db.TimeTrackings.Include(timeTracking => timeTracking.CalendarEvent).Single();
        Assert.NotNull(row.CalendarEvent);
        Assert.True(row.CalendarEvent!.CalendarProvider == CalendarProvider.Outlook365, "Provider persisted.");
        Assert.True(row.CalendarEvent.AccountKey == Account, "Account persisted.");
        Assert.True(row.CalendarEvent.ExternalEventId == EventId, "External id persisted.");
    }

    [Fact]
    public async Task Create_ManualEntry_HasNoReference()
    {
        await using var factory = new CalendarStubApiFactory();
        var (seeded, projectId) = await SetupAsync(factory);

        var response = await PostCreateAsync(seeded, CreateForm(projectId));
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}.");

        await using var db = seeded.NewDbContext();
        Assert.True(db.TimeTrackings.Include(timeTracking => timeTracking.CalendarEvent).Single().CalendarEvent is null, "Manual entries carry no reference.");
    }

    [Fact]
    public async Task Create_DuplicateEventReference_IsRejected()
    {
        await using var factory = new CalendarStubApiFactory();
        var (seeded, projectId) = await SetupAsync(factory);

        await using (var db = seeded.NewDbContext())
        {
            db.TimeTrackings.Add(new TimeTracking
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                StartedAtUtc = new DateTime(2026, 10, 13, 6, 45, 0, DateTimeKind.Utc),
                DurationInMinutes = 45,
                Description = "first",
                CalendarEvent = new CalendarEventReference(CalendarProvider.Outlook365, Account, EventId),
                CreatedAtUtc = new DateTime(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc)
            });
            await db.SaveChangesAsync(Ct);
        }

        var form = CreateForm(projectId);
        form["EventProviderKey"] = "outlook365";
        form["EventAccountKey"] = Account;
        form["EventId"] = EventId;

        var response = await PostCreateAsync(seeded, form);
        Assert.True(response.IsSuccessStatusCode, $"Expected toast response, got {(int)response.StatusCode}.");
        // Toasts arrive through the HX-Trigger header JSON, not the (empty) body.
        Assert.True(response.Headers.TryGetValues("HX-Trigger", out var triggers));
        Assert.Contains("already logged", string.Join(",", triggers));

        await using var checkDb = seeded.NewDbContext();
        Assert.Equal(1, checkDb.TimeTrackings.Count());
    }

    [Fact]
    public async Task Create_PartialEventReference_IsRejected()
    {
        await using var factory = new CalendarStubApiFactory();
        var (seeded, projectId) = await SetupAsync(factory);

        var form = CreateForm(projectId);
        form["EventId"] = EventId;   // provider/account omitted -> all-or-nothing violation

        var response = await PostCreateAsync(seeded, form);
        Assert.True(response.IsSuccessStatusCode, "Validation failure re-renders the dialog.");
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("alert-danger", content);

        await using var db = seeded.NewDbContext();
        Assert.Equal(0, db.TimeTrackings.Count());
    }

    [Fact]
    public async Task CalendarPanel_UntrackedEvent_IsClickableThroughSingleUri()
    {
        await using var factory = new CalendarStubApiFactory();
        await SetupAsync(factory);
        factory.Reader.Result = new CalendarReadResult(true, [Standup()], null);

        var html = await factory.CreateClient().GetStringAsync("/time-tracking/list");

        var panelStart = html.IndexOf("tt-calendar-panel", StringComparison.Ordinal);
        Assert.True(panelStart >= 0, "Calendar panel should be rendered.");
        var panel = html[panelStart..];
        Assert.Contains("hx-get=\"/time-tracking/event?", panel);
        Assert.Contains("#dialog-host", panel);
        Assert.DoesNotContain("hx-post", panel);
        Assert.DoesNotContain("tt-event-tracked-badge", panel);
    }

    [Fact]
    public async Task CalendarPanel_TtrackedEvent_ShowsLoggedBadge()
    {
        await using var factory = new CalendarStubApiFactory();
        var (seeded, projectId) = await SetupAsync(factory);
        factory.Reader.Result = new CalendarReadResult(true, [Standup()], null);

        await using (var db = seeded.NewDbContext())
        {
            db.TimeTrackings.Add(new TimeTracking
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                StartedAtUtc = new DateTime(2026, 10, 13, 6, 45, 0, DateTimeKind.Utc),
                DurationInMinutes = 45,
                Description = "standup",
                CalendarEvent = new CalendarEventReference(CalendarProvider.Outlook365, Account, EventId),
                CreatedAtUtc = new DateTime(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc)
            });
            await db.SaveChangesAsync(Ct);
        }

        var html = await seeded.CreateClient().GetStringAsync("/time-tracking/list");

        Assert.Contains("tt-event-tracked-badge", html);
        Assert.Contains("Logged", html);
        // Same single URI either way (server decides the dialog).
        Assert.Contains("hx-get=\"/time-tracking/event?", html);
    }
}
