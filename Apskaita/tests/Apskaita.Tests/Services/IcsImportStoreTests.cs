using System.Security.Claims;
using Apskaita.Entities;
using Apskaita.Feature.Account;
using Apskaita.Feature.Account.Persistence;
using Apskaita.Feature.Account.Services;
using Apskaita.Shared.Calendar;
using Apskaita.Web.Data;
using Microsoft.AspNetCore.Http;

namespace Apskaita.Tests.Services;

/// <summary>
/// Unit tests for the imported (.ics) calendar store and reader: a named connector is created once and
/// survives every import (only its event rows are replaced), statistics describe what it currently holds,
/// and the reader turns stored UTC rows into the panel's display-zone events.
/// </summary>
public sealed class IcsImportStoreTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly Guid Owner = Guid.NewGuid();

    private static (ApskaitaDbContext db, IcsConnectorStore store) Build(string? cookie = null)
    {
        var db = TestDb.New();
        var accessor = new SessionAccessor("ics@example.com", Owner, cookie);
        var connectors = new CalendarConnectorStore(new TestAccountStore(db), accessor);
        return (db, new IcsConnectorStore(new TestAccountStore(db), connectors));
    }

    private static IcsParsedEvent Event(string uid, DateTime startUtc, DateTime? endUtc, string subject = "Event") =>
        new(uid, startUtc, endUtc, subject, null, null);

    [Fact]
    public async Task Create_StoresTheEnteredNameAsTheAccountKey()
    {
        var (db, store) = Build();

        var (connector, error) = await store.CreateOwnAsync("  NrdCompanies  ", Ct);

        Assert.Null(error);
        Assert.NotNull(connector);
        Assert.Equal(CalendarProvider.Ics, connector.Provider);
        Assert.Equal("nrdcompanies", connector.AccountKey);
        Assert.Equal("NrdCompanies", connector.DisplayName);
        Assert.Equal("NrdCompanies", connector.AccountEmail);
        Assert.Equal(string.Empty, connector.RefreshTokenProtected);
        Assert.Equal(IcsConnectorStore.ImportScopes, connector.Scopes);
        Assert.Equal(1, db.CalendarConnectors.Count());
    }

    [Fact]
    public async Task Create_RejectsADuplicateName_CaseInsensitively()
    {
        var (_, store) = Build();
        await store.CreateOwnAsync("nrdcompanies", Ct);

        var (connector, error) = await store.CreateOwnAsync("NRDCOMPANIES", Ct);

        Assert.Null(connector);
        Assert.Contains("already exists", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("bad/name")]
    [InlineData("<script>")]
    public async Task Create_RejectsInvalidNames(string? name)
    {
        var (db, store) = Build();

        var (connector, error) = await store.CreateOwnAsync(name, Ct);

        Assert.Null(connector);
        Assert.NotEmpty(error!);
        Assert.Empty(db.CalendarConnectors);
    }

    [Fact]
    public async Task Create_RejectsANameBeyondTheLimit()
    {
        var (_, store) = Build();

        var (connector, error) = await store.CreateOwnAsync(new string('n', FieldLengths.CalendarConnector.IcsName + 1), Ct);

        Assert.Null(connector);
        Assert.Contains("characters", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_AllowsSeveralNamedCalendars()
    {
        var (db, store) = Build();

        var first = await store.CreateOwnAsync("nrdcompanies", Ct);
        var second = await store.CreateOwnAsync("othercompany", Ct);

        Assert.Null(first.Error);
        Assert.Null(second.Error);
        Assert.Equal(2, db.CalendarConnectors.Count());
    }

    [Fact]
    public async Task Import_ReplacesOnlyTheEvents_TheConnectorSurvives()
    {
        var (db, store) = Build();
        var (connector, _) = await store.CreateOwnAsync("nrdcompanies", Ct);
        var created = connector!;
        var createdAt = created.ConnectedAtUtc;

        await store.ImportAsync(created, [Event("a", new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc), null)], Ct);
        await store.ImportAsync(created,
            [
                Event("b", new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc), null),
                Event("c", new DateTime(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc), null)
            ], Ct);

        Assert.Equal(created.Id, (await store.GetOwnByIdAsync(created.Id, Ct))!.Id);
        Assert.Equal(2, db.CalendarImportedEvents.Count());
        Assert.All(db.CalendarImportedEvents.ToArray(), e => Assert.Equal(created.Id, e.ConnectorId));
        Assert.DoesNotContain(db.CalendarImportedEvents.ToArray(), e => e.ExternalEventId == "a");
        Assert.NotNull(db.CalendarConnectors.Single(c => c.Id == created.Id).LastSyncAtUtc);
        Assert.True(db.CalendarConnectors.Single(c => c.Id == created.Id).ConnectedAtUtc >= createdAt);
    }

    [Fact]
    public async Task Import_PersistsTheSeriesGroup_ForExpandedOccurrences()
    {
        var (db, store) = Build();
        var (connector, _) = await store.CreateOwnAsync("nrdcompanies", Ct);

        await store.ImportAsync(connector!,
        [
            Event("standup@20261019T090000Z", new DateTime(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc), null, "Standup") with { GroupId = "standup" }
        ], Ct);

        var row = Assert.Single(db.CalendarImportedEvents.ToArray());
        Assert.Equal("standup@20261019T090000Z", row.ExternalEventId);
        Assert.Equal("standup", row.GroupId);
    }

    [Fact]
    public async Task DeleteImportedCalendar_KeepsTimeEntryReference()
    {
        var (db, store) = Build();
        var (connector, _) = await store.CreateOwnAsync("nrdcompanies", Ct);
        await store.ImportAsync(connector!,
            [Event("uid-1", new DateTime(2026, 10, 13, 6, 45, 0, DateTimeKind.Utc), null)], Ct);

        db.TimeTrackings.Add(new TimeTracking
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            StartedAtUtc = new DateTime(2026, 10, 13, 6, 45, 0, DateTimeKind.Utc),
            DurationInMinutes = 45,
            Description = "from ics",
            CalendarEvent = new CalendarEventReference(CalendarProvider.Ics, "nrdcompanies", "uid-1"),
            CreatedAtUtc = new DateTime(2026, 10, 13, 7, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 10, 13, 7, 0, 0, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync(Ct);

        // Deleting the imported calendar removes its connector and events but never the entry's reference.
        db.CalendarConnectors.Remove(connector!);
        await db.SaveChangesAsync(Ct);

        Assert.Empty(db.CalendarConnectors.ToArray());
        Assert.Empty(db.CalendarImportedEvents.ToArray());
        var kept = Assert.Single(db.TimeTrackings.ToArray());
        Assert.NotNull(kept.CalendarEvent);
        Assert.Equal(CalendarProvider.Ics, kept.CalendarEvent!.CalendarProvider);
        Assert.Equal("nrdcompanies", kept.CalendarEvent.AccountKey);
        Assert.Equal("uid-1", kept.CalendarEvent.ExternalEventId);
    }

    [Fact]
    public async Task ReimportSameCalendar_RelinksTheExistingTimeEntry()
    {
        var (db, store) = Build();
        var (connector, _) = await store.CreateOwnAsync("nrdcompanies", Ct);
        await store.ImportAsync(connector!,
            [Event("uid-1", new DateTime(2026, 10, 13, 6, 45, 0, DateTimeKind.Utc), null)], Ct);

        db.TimeTrackings.Add(new TimeTracking
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            StartedAtUtc = new DateTime(2026, 10, 13, 6, 45, 0, DateTimeKind.Utc),
            DurationInMinutes = 45,
            Description = "from ics",
            CalendarEvent = new CalendarEventReference(CalendarProvider.Ics, "nrdcompanies", "uid-1"),
            CreatedAtUtc = new DateTime(2026, 10, 13, 7, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 10, 13, 7, 0, 0, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync(Ct);

        db.CalendarConnectors.Remove(connector!);
        await db.SaveChangesAsync(Ct);

        // Re-create the same named calendar and re-import the same event: the identity triple matches again,
        // so the existing entry is tracked against the re-imported event.
        var (again, _) = await store.CreateOwnAsync("nrdcompanies", Ct);
        await store.ImportAsync(again!,
            [Event("uid-1", new DateTime(2026, 10, 13, 6, 45, 0, DateTimeKind.Utc), null)], Ct);

        var reimported = Assert.Single(db.CalendarImportedEvents.ToArray());
        var entry = Assert.Single(db.TimeTrackings.ToArray());
        Assert.Equal(reimported.ExternalEventId, entry.CalendarEvent!.ExternalEventId);
        Assert.Equal(again!.AccountKey, entry.CalendarEvent.AccountKey);

        var tracked = db.TimeTrackings.ToArray().Any(t => t.CalendarEvent is { } r
            && r.CalendarProvider == CalendarProvider.Ics
            && r.AccountKey == again.AccountKey
            && r.ExternalEventId == reimported.ExternalEventId);
        Assert.True(tracked);
    }

    [Fact]
    public async Task GetStats_ReportsCountAndCoveredRange()
    {
        var (_, store) = Build();
        var (connector, _) = await store.CreateOwnAsync("nrdcompanies", Ct);

        await store.ImportAsync(connector!,
        [
            Event("a", new DateTime(2026, 3, 2, 8, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc)),
            Event("b", new DateTime(2026, 9, 30, 8, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 20, 10, 0, 0, DateTimeKind.Utc))
        ], Ct);

        var stats = await store.GetStatsAsync([connector!.Id], Ct);

        Assert.Equal(2, stats[connector.Id].EventCount);
        Assert.Equal(new DateTime(2026, 3, 2, 8, 0, 0, DateTimeKind.Utc), stats[connector.Id].FirstStartUtc);
        Assert.Equal(new DateTime(2026, 12, 20, 10, 0, 0, DateTimeKind.Utc), stats[connector.Id].LastEndUtc);
    }

    [Fact]
    public async Task GetStats_ForAnEmptyCalendar_IsZeroWithoutDates()
    {
        var (_, store) = Build();
        var (connector, _) = await store.CreateOwnAsync("empty", Ct);

        var stats = await store.GetStatsAsync([connector!.Id], Ct);

        Assert.Equal(0, stats[connector.Id].EventCount);
        Assert.Null(stats[connector.Id].FirstStartUtc);
    }

    [Fact]
    public async Task GetOwnById_IgnoresAnotherUsersConnector()
    {
        var (db, store) = Build();
        var (connector, _) = await store.CreateOwnAsync("nrdcompanies", Ct);

        var other = new CalendarConnectorStore(new TestAccountStore(db), new SessionAccessor("other@example.com", Guid.NewGuid()));
        var otherStore = new IcsConnectorStore(new TestAccountStore(db), other);

        Assert.Null(await otherStore.GetOwnByIdAsync(connector!.Id, Ct));
        Assert.NotNull(await store.GetOwnByIdAsync(connector.Id, Ct));
    }

    [Fact]
    public async Task Reader_MapsStoredRows_ToDisplayZoneEvents()
    {
        // Browser is three hours ahead of UTC: 06:45 UTC renders as 09:45.
        var (db, store) = Build("tz-offset=180");
        var (connector, _) = await store.CreateOwnAsync("nrdcompanies", Ct);

        await store.ImportAsync(connector!, [
            Event("a", new DateTime(2026, 10, 13, 6, 45, 0, DateTimeKind.Utc), new DateTime(2026, 10, 13, 7, 30, 0, DateTimeKind.Utc), "Morning sync")
        ], Ct);

        var reader = new IcsEventReader(store, new TestQuery<CalendarImportedEvent>(db), new SessionAccessor("ics@example.com", Owner, "tz-offset=180"));
        var result = await reader.ReadAsync(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18), Ct);

        Assert.True(result.HasConnector);
        var evt = Assert.Single(result.Events);
        Assert.Equal("Morning sync", evt.Subject);
        Assert.Equal(new DateTime(2026, 10, 13, 9, 45, 0), evt.StartLocal);
        Assert.Equal(new DateTime(2026, 10, 13, 10, 30, 0), evt.EndLocal);
        Assert.Equal("ics", evt.ProviderKey);
        Assert.Equal("nrdcompanies", evt.AccountKey);
        Assert.Equal("a", evt.EventId);
    }

    [Fact]
    public async Task Reader_AllDayEvent_KeepsItsDateAndSpansTheDay()
    {
        var (db, store) = Build("tz-offset=180");
        var (connector, _) = await store.CreateOwnAsync("holidays", Ct);

        await store.ImportAsync(connector!, [
            Event("holiday", new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc), null, "National day")
        ], Ct);

        var reader = new IcsEventReader(store, new TestQuery<CalendarImportedEvent>(db), new SessionAccessor("ics@example.com", Owner, "tz-offset=180"));
        var result = await reader.ReadAsync(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18), Ct);

        var evt = Assert.Single(result.Events);
        Assert.Equal(new DateTime(2026, 10, 14, 0, 0, 0), evt.StartLocal);
        Assert.Equal(new DateTime(2026, 10, 15, 0, 0, 0), evt.EndLocal);
    }

    [Fact]
    public async Task Reader_WithoutConnectors_ReportsNoConnection()
    {
        var (db, store) = Build();
        var reader = new IcsEventReader(store, new TestQuery<CalendarImportedEvent>(db), new SessionAccessor("ics@example.com", Owner));

        var result = await reader.ReadAsync(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18), Ct);

        Assert.False(result.HasConnector);
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task CompositeReader_MergesProviders_SortedByLocalStart()
    {
        var outlook = new FixedReader(new(true, [new("Outlook event", new DateTime(2026, 10, 14, 12, 0, 0), new DateTime(2026, 10, 14, 13, 0, 0), null, false, null, "outlook365", "me@contoso.com", "oid-1")], null));
        var ics = new FixedReader(new(true, [new("Imported event", new DateTime(2026, 10, 14, 9, 0, 0), new DateTime(2026, 10, 14, 9, 30, 0), "Vilnius", false, null, "ics", "nrdcompanies", "uid-1")], null));

        var result = await new CompositeCalendarEventReader([outlook, ics]).ReadAsync(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18), Ct);

        Assert.True(result.HasConnector);
        Assert.Equal(["Imported event", "Outlook event"], result.Events.Select(e => e.Subject).ToArray());
        Assert.Null(result.Notice);
    }

    [Fact]
    public async Task CompositeReader_KeepsTheWorkingProvider_WhenTheOtherFails()
    {
        var broken = new ThrowingReader();
        var working = new FixedReader(new(true, [new("Imported", new DateTime(2026, 10, 14, 9, 0, 0), new DateTime(2026, 10, 14, 9, 30, 0), null, false, null, "ics", "nrdcompanies", "uid-1")], "Provider unavailable"));

        var result = await new CompositeCalendarEventReader([broken, working]).ReadAsync(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18), Ct);

        Assert.True(result.HasConnector);
        Assert.Single(result.Events);
        Assert.Equal("Provider unavailable", result.Notice);
    }

    [Fact]
    public async Task CompositeReader_WithoutAnyConnection_ReportsNone()
    {
        var result = await new CompositeCalendarEventReader(
            [
                new FixedReader(new(false, [], null)),
                new FixedReader(new(false, [], "ignored while nothing is connected"))
            ])
            .ReadAsync(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18), Ct);

        Assert.False(result.HasConnector);
        Assert.Null(result.Notice);
    }

    private sealed class FixedReader(CalendarReadResult result) : ICalendarEventReader
    {
        public Task<CalendarReadResult> ReadAsync(DateOnly fromLocal, DateOnly toLocal, CancellationToken ct) => Task.FromResult(result);
    }

    private sealed class ThrowingReader : ICalendarEventReader
    {
        public Task<CalendarReadResult> ReadAsync(DateOnly fromLocal, DateOnly toLocal, CancellationToken ct) =>
            throw new InvalidOperationException("provider exploded");
    }

    private sealed class SessionAccessor(string email, Guid userId, string? cookie = null) : IHttpContextAccessor
    {
        public HttpContext? HttpContext
        {
            get
            {
                var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Name, email),
                    new Claim(AuthConstants.UserIdClaim, userId.ToString())
                ], "Test", ClaimTypes.Name, ClaimTypes.Role);

                var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
                if (cookie is not null)
                {
                    context.Request.Headers["Cookie"] = cookie;
                }

                return context;
            }
            set { }
        }
    }
}
