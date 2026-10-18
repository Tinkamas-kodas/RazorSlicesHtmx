using System.Net;
using Apskaita.Entities;
using Apskaita.Feature.WorkActs.Models;
using Apskaita.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Apskaita.Tests.Endpoints;

/// <summary>
/// add-work-acts (line-based composition): the act list and its derived statuses, the create flow (review
/// with draft lines, transient row edits, act + lines in one save), Draft act editing, the Final freeze,
/// deletion rules, and the time-tracking side of the rule — an entry referenced by a work act line is
/// locked.
/// <para>
/// The test host has no <c>tz-offset</c> cookie, so the display zone is the server's local zone; the
/// period boundaries below are computed with the same offset the endpoints use.
/// </para>
/// </summary>
[Collection("Integration")] // in-process hosts must not run concurrently (ApskaitaApiFactory race)
public sealed class WorkActsTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly TimeSpan Offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);

    private static DateTime WallToUtc(DateTime wallClock) => wallClock - Offset;

    private static async Task<(ApskaitaApiFactory factory, Guid projectId, Guid otherProjectId)> SetupAsync(
        int entryCount = 3,
        int otherEntryCount = 0)
    {
        var factory = new ApskaitaApiFactory();
        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();

        await using var db = factory.NewDbContext();

        db.Projects.Add(new Project
        {
            Id = projectId,
            ClientId = Guid.NewGuid(),
            InvoiceTemplateId = Guid.NewGuid(),
            Name = "Karma",
            Code = "KRM",
            HourlyRate = 100,
            ColorRgb = "#F4B6C2",
            CurrencyCode = "EUR",
            IsActive = true
        });
        db.Projects.Add(new Project
        {
            Id = otherProjectId,
            ClientId = Guid.NewGuid(),
            InvoiceTemplateId = Guid.NewGuid(),
            Name = "Zebra",
            HourlyRate = 50,
            ColorRgb = "#AAAAAA",
            CurrencyCode = "EUR",
            IsActive = true
        });

        // Entries on the 5th, 6th and 7th of October 2026, one hour each.
        for (var i = 0; i < entryCount; i++)
        {
            db.TimeTrackings.Add(Entry(projectId, new DateTime(2026, 10, 5 + i, 9, 0, 0, DateTimeKind.Unspecified), 60, $"work {i}"));
        }

        for (var i = 0; i < otherEntryCount; i++)
        {
            db.TimeTrackings.Add(Entry(otherProjectId, new DateTime(2026, 10, 5 + i, 9, 0, 0, DateTimeKind.Unspecified), 60, $"other {i}"));
        }

        await db.SaveChangesAsync(Ct);
        return (factory, projectId, otherProjectId);
    }

    private static TimeTracking Entry(Guid projectId, DateTime wallStart, int minutes, string description) => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = projectId,
        StartedAtUtc = WallToUtc(wallStart),
        DurationInMinutes = minutes,
        Description = description,
        CreatedAtUtc = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc),
        UpdatedAtUtc = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc)
    };

    private static WorkAct NewAct(Guid projectId, WorkActStatus status = WorkActStatus.Draft) => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = projectId,
        ClientId = Guid.NewGuid(),
        CurrencyCode = "EUR",
        PeriodStartUtc = WallToUtc(new DateTime(2026, 10, 5)),
        PeriodEndUtc = WallToUtc(new DateTime(2026, 10, 8)),
        TotalHours = 3m,
        TotalAmount = 300m,
        Status = status,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };

    /// <summary>Seeds an act with one line per entry id directly (bypassing the create flow).</summary>
    private static async Task<(WorkAct act, List<WorkActLine> lines)> CreateActWithLinesAsync(
        ApskaitaApiFactory factory, Guid projectId, Guid[] entryIds, WorkActStatus status = WorkActStatus.Draft)
    {
        await using var db = factory.NewDbContext();

        var starts = db.TimeTrackings.Where(t => entryIds.Contains(t.Id))
            .OrderBy(t => t.StartedAtUtc)
            .Select(t => new { t.Id, t.StartedAtUtc, t.DurationInMinutes, t.Description })
            .ToList();

        var act = NewAct(projectId, status);
        var lines = starts
            .Select(e => new WorkActLine
            {
                Id = Guid.NewGuid(),
                WorkActId = act.Id,
                TimeTrackingId = e.Id,
                DurationMinutes = e.DurationInMinutes,
                HourlyRate = 100,
                StartedAtUtc = e.StartedAtUtc,
                Description = e.Description,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            })
            .ToList();

        db.WorkActs.Add(act);
        db.WorkActLines.AddRange(lines);
        await db.SaveChangesAsync(Ct);

        return (act, lines);
    }

    /// <summary>Runs the real create flow: the dialog posts the period and the server snapshots its entries.</summary>
    private static async Task<HttpResponseMessage> CreateActViaPostAsync(
        ApskaitaApiFactory factory, Guid projectId, string from = "2026-10-05", string to = "2026-10-07")
    {
        string code;
        await using (var seed = factory.NewDbContext())
        {
            code = seed.Projects.First(p => p.Id == projectId).Code;
        }

        return await PostFormAsync(factory, "/work-acts/create", new Dictionary<string, string>
        {
            ["ProjectCode"] = code,
            ["DateFrom"] = from,
            ["DateTo"] = to
        });
    }

    private static async Task<HttpResponseMessage> PostFormAsync(
        ApskaitaApiFactory factory, string url, Dictionary<string, string> form, string tokenPage = "/work-acts")
    {
        var http = factory.CreateClient();
        var token = await TestAntiforgery.GetTokenAsync(http, tokenPage);
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(form)
        };
        request.Headers.TryAddWithoutValidation("RequestVerificationToken", token);
        request.Headers.TryAddWithoutValidation("HX-Request", "true");
        request.Headers.TryAddWithoutValidation("HX-Target", "dialog-host");
        return await http.SendAsync(request);
    }

    // ---- create dialog defaults ----

    [Fact]
    public void CreateDefault_PrefillsPreviousMonth()
    {
        var request = WorkActCreateRequest.CreateDefault();

        var today = DateOnly.FromDateTime(DateTime.Now);
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);

        Assert.Equal(firstOfMonth.AddMonths(-1), request.DateFrom);
        Assert.Equal(firstOfMonth.AddDays(-1), request.DateTo);
    }

    // ---- list ----

    [Fact]
    public async Task List_DefaultOrder_IsPeriodEndDescending()
    {
        var (factory, projectId, _) = await SetupAsync();

        await using var seed = factory.NewDbContext();
        var older = NewAct(projectId);
        older.PeriodStartUtc = WallToUtc(new DateTime(2026, 1, 1));
        older.PeriodEndUtc = WallToUtc(new DateTime(2026, 2, 1));
        var newer = NewAct(projectId);
        newer.PeriodStartUtc = WallToUtc(new DateTime(2026, 9, 1));
        newer.PeriodEndUtc = WallToUtc(new DateTime(2026, 10, 1));
        seed.WorkActs.AddRange([older, newer]);
        await seed.SaveChangesAsync(Ct);

        var html = await TestHtmx.GetHtmlAsync(factory.CreateClient(), "/work-acts/list", "work-acts-list");

        var newerAt = html.IndexOf(newer.Id.ToString(), StringComparison.Ordinal);
        var olderAt = html.IndexOf(older.Id.ToString(), StringComparison.Ordinal);

        Assert.True(newerAt >= 0 && olderAt >= 0, "Both acts should be listed.");
        Assert.True(newerAt < olderAt, "The act with the later period end must come first.");
    }

    [Fact]
    public async Task List_FilteredByOneProject_ReturnsOnlyItsActs()
    {
        var (factory, projectId, otherProjectId) = await SetupAsync();

        await using var seed = factory.NewDbContext();
        var karmaAct = NewAct(projectId);
        var zebraAct = NewAct(otherProjectId);
        seed.WorkActs.AddRange([karmaAct, zebraAct]);
        await seed.SaveChangesAsync(Ct);

        var html = await TestHtmx.GetHtmlAsync(
            factory.CreateClient(), $"/work-acts/list?Projects={projectId}", "work-acts-list");

        Assert.Contains(karmaAct.Id.ToString(), html);
        Assert.DoesNotContain(zebraAct.Id.ToString(), html);
    }

    [Fact]
    public async Task List_FilteredByTwoProjects_ReturnsBoth()
    {
        var (factory, projectId, otherProjectId) = await SetupAsync();

        await using var seed = factory.NewDbContext();
        var karmaAct = NewAct(projectId);
        var zebraAct = NewAct(otherProjectId);
        seed.WorkActs.AddRange([karmaAct, zebraAct]);
        await seed.SaveChangesAsync(Ct);

        var html = await TestHtmx.GetHtmlAsync(
            factory.CreateClient(), $"/work-acts/list?Projects={projectId}&Projects={otherProjectId}", "work-acts-list");

        Assert.Contains(karmaAct.Id.ToString(), html);
        Assert.Contains(zebraAct.Id.ToString(), html);
    }

    [Fact]
    public async Task List_FinalActReferencedByInvoice_ShowsInvoiced()
    {
        var (factory, projectId, _) = await SetupAsync();

        Guid actId;
        await using (var seed = factory.NewDbContext())
        {
            var act = NewAct(projectId, WorkActStatus.Final);
            seed.WorkActs.Add(act);
            seed.InvoiceLines.Add(new InvoiceLine
            {
                Id = Guid.NewGuid(),
                InvoiceId = Guid.NewGuid(),
                LineNo = 1,
                SourceType = InvoiceLineSourceType.WorkAct,
                WorkActId = act.Id,
                Description = "act",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            actId = act.Id;
            await seed.SaveChangesAsync(Ct);
        }

        var html = await TestHtmx.GetHtmlAsync(factory.CreateClient(), "/work-acts/list", "work-acts-list");

        Assert.Contains(actId.ToString(), html);
        Assert.Contains("Invoiced", html);
    }

    // ---- review ----

    [Fact]
    public async Task Review_ListsTheActsLines_AndExcludesTheExclusiveEnd()
    {
        var (factory, projectId, otherProjectId) = await SetupAsync(entryCount: 3, otherEntryCount: 1);

        // An entry starting exactly at the period end instant (local midnight after the last day) is out.
        await using (var seed = factory.NewDbContext())
        {
            seed.TimeTrackings.Add(Entry(projectId, new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Unspecified), 30, "boundary"));
            await seed.SaveChangesAsync(Ct);
        }

        Assert.True((await CreateActViaPostAsync(factory, projectId)).IsSuccessStatusCode);

        Guid actId;
        await using (var seed = factory.NewDbContext())
        {
            actId = seed.WorkActs.Single().Id;
            Assert.Equal(3, seed.WorkActLines.Count(l => l.WorkActId == actId));
        }

        var html = await TestHtmx.GetHtmlAsync(
            factory.CreateClient(), $"/work-acts/review/{actId}", "work-acts-detail");

        Assert.Contains("work 0", html);
        Assert.Contains("work 2", html);
        Assert.DoesNotContain("other 0", html);
        Assert.DoesNotContain("boundary", html);
    }

    [Fact]
    public async Task Review_MarksLinesWhoseEntryIsInAnotherAct_WithTooltip()
    {
        var (factory, projectId, _) = await SetupAsync();

        Guid[] entryIds;
        await using (var seed = factory.NewDbContext())
        {
            entryIds = seed.TimeTrackings.OrderBy(t => t.StartedAtUtc).Select(t => t.Id).ToArray();
        }

        // An earlier act already references the first entry; the new act's line for it is shared.
        await CreateActWithLinesAsync(factory, projectId, [entryIds[0]]);
        Assert.True((await CreateActViaPostAsync(factory, projectId)).IsSuccessStatusCode);

        Guid actId;
        await using (var seed = factory.NewDbContext())
        {
            actId = seed.WorkActs.OrderByDescending(a => a.CreatedAtUtc).ThenByDescending(a => a.Id).First().Id;
        }

        var html = await TestHtmx.GetHtmlAsync(
            factory.CreateClient(), $"/work-acts/review/{actId}", "work-acts-detail");

        Assert.Contains("is-shared", html);
        Assert.Contains("Also in:", html);
    }

    // ---- create ----

    [Fact]
    public async Task CreateDialog_PostValid_CreatesActAndNavigatesToReview()
    {
        var (factory, _, _) = await SetupAsync();

        var response = await PostFormAsync(factory, "/work-acts/create", new Dictionary<string, string>
        {
            ["ProjectCode"] = "KRM",
            ["DateFrom"] = "2026-10-05",
            ["DateTo"] = "2026-10-07"
        });

        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}.");
        Assert.True(response.Headers.TryGetValues("HX-Location", out var locations));

        await using var db = factory.NewDbContext();
        var act = db.WorkActs.Single();
        Assert.Contains($"/work-acts/review/{act.Id}", string.Join(",", locations));
        Assert.Equal(3, db.WorkActLines.Count(l => l.WorkActId == act.Id));
    }

    [Fact]
    public async Task CreateDialog_PostInvalid_RendersDialogWithInlineErrors()
    {
        var (factory, _, _) = await SetupAsync();

        var response = await PostFormAsync(factory, "/work-acts/create", new Dictionary<string, string>
        {
            ["ProjectCode"] = "",
            ["DateFrom"] = "2026-10-05",
            ["DateTo"] = "2026-10-04"
        });

        Assert.True(response.IsSuccessStatusCode, $"Expected the dialog re-render, got {(int)response.StatusCode}.");
        Assert.False(response.Headers.Contains("HX-Location"), "Invalid input must not navigate.");

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Project is required", content);
        Assert.Contains("The end date must not be before the start date", content);
        Assert.Contains("is-invalid", content);

        await using var db = factory.NewDbContext();
        Assert.Empty(db.WorkActs);
    }

    [Fact]
    public async Task Create_WithNoEntriesInPeriod_CreatesNothingAndShowsToast()
    {
        var (factory, projectId, _) = await SetupAsync();

        var response = await CreateActViaPostAsync(factory, projectId, "2026-01-01", "2026-01-31");

        Assert.True(response.IsSuccessStatusCode, $"Expected a toast response, got {(int)response.StatusCode}.");
        Assert.True(response.Headers.TryGetValues("HX-Trigger", out var triggers));
        Assert.Contains("nothing to certify", string.Join(",", triggers));

        await using var db = factory.NewDbContext();
        Assert.Empty(db.WorkActs);
    }

    [Fact]
    public async Task Create_ValidPeriod_StoresActAndLines()
    {
        var (factory, projectId, _) = await SetupAsync();

        var response = await CreateActViaPostAsync(factory, projectId);

        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}.");
        // Success navigates to the review page of the new act via HX-Location.
        Assert.True(response.Headers.TryGetValues("HX-Location", out var locations));
        Assert.Contains("/work-acts/review/", string.Join(",", locations));

        await using var db = factory.NewDbContext();
        var act = db.WorkActs.Single();
        Assert.Equal(3m, act.TotalHours);
        Assert.Equal(300m, act.TotalAmount);
        Assert.True(act.Status == WorkActStatus.Draft);

        var entryIds = db.TimeTrackings.Select(t => t.Id).ToArray();
        Assert.Equal(3, db.WorkActLines.Count(l => l.WorkActId == act.Id && entryIds.Contains(l.TimeTrackingId)));
    }

    [Fact]
    public async Task Create_SameEntriesTwice_BothActsExistWithTheirOwnLines()
    {
        var (factory, projectId, _) = await SetupAsync();

        var first = await CreateActViaPostAsync(factory, projectId);
        Assert.True(first.IsSuccessStatusCode, $"Expected success, got {(int)first.StatusCode}.");

        var second = await CreateActViaPostAsync(factory, projectId);
        Assert.True(second.IsSuccessStatusCode, $"Multi-act membership is allowed, got {(int)second.StatusCode}.");

        await using var db = factory.NewDbContext();
        Assert.Equal(2, db.WorkActs.Count());
        Assert.Equal(6, db.WorkActLines.Count());
    }

    // ---- edit and finalize ----

    [Fact]
    public async Task Edit_DraftAct_LineEditPersistsAndRecomputesTotals()
    {
        var (factory, projectId, _) = await SetupAsync();
        Assert.True((await CreateActViaPostAsync(factory, projectId)).IsSuccessStatusCode);

        Guid actId;
        Guid lineId;
        await using (var seed = factory.NewDbContext())
        {
            actId = seed.WorkActs.Single().Id;
            lineId = seed.WorkActLines.OrderBy(l => l.StartedAtUtc).First().Id;
        }

        var response = await PostFormAsync(factory, "/work-acts/edit/line", new Dictionary<string, string>
        {
            ["ActId"] = actId.ToString(),
            ["LineId"] = lineId.ToString(),
            ["DurationMinutes"] = "120",
            ["Description"] = "edited line",
            ["HourlyRate"] = "100"
        });

        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}.");
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("edited line", content);
        Assert.Contains("wa-totals", content);

        await using var db = factory.NewDbContext();
        var line = await db.WorkActLines.AsNoTracking().FirstAsync(l => l.Id == lineId, Ct);
        Assert.Equal(120, line.DurationMinutes);
        Assert.Equal("edited line", line.Description);

        var act = await db.WorkActs.AsNoTracking().FirstAsync(a => a.Id == actId, Ct);
        Assert.Equal(4m, act.TotalHours);
        Assert.Equal(400m, act.TotalAmount);
    }

    [Fact]
    public async Task Edit_LineEdit_DoesNotTouchTheTimeEntry()
    {
        var (factory, projectId, _) = await SetupAsync();
        Assert.True((await CreateActViaPostAsync(factory, projectId)).IsSuccessStatusCode);

        Guid actId;
        Guid lineId;
        Guid entryId;
        await using (var seed = factory.NewDbContext())
        {
            actId = seed.WorkActs.Single().Id;
            var line = seed.WorkActLines.OrderBy(l => l.StartedAtUtc).First();
            lineId = line.Id;
            entryId = line.TimeTrackingId;
        }

        await PostFormAsync(factory, "/work-acts/edit/line", new Dictionary<string, string>
        {
            ["ActId"] = actId.ToString(),
            ["LineId"] = lineId.ToString(),
            ["DurationMinutes"] = "30",
            ["Description"] = "line only",
            ["HourlyRate"] = "77"
        });

        await using var db = factory.NewDbContext();
        var entry = await db.TimeTrackings.AsNoTracking().FirstAsync(t => t.Id == entryId, Ct);
        Assert.Equal(60, entry.DurationInMinutes);
        Assert.NotEqual("line only", entry.Description);
    }

    [Fact]
    public async Task Final_DraftAct_FreezesItsLines()
    {
        var (factory, projectId, _) = await SetupAsync();
        Assert.True((await CreateActViaPostAsync(factory, projectId)).IsSuccessStatusCode);

        Guid actId;
        Guid lineId;
        await using (var seed = factory.NewDbContext())
        {
            actId = seed.WorkActs.Single().Id;
            lineId = seed.WorkActLines.First().Id;
        }

        var finalize = await PostFormAsync(factory, $"/work-acts/final/{actId}", []);
        Assert.True(finalize.IsSuccessStatusCode, $"Expected success, got {(int)finalize.StatusCode}.");

        await using (var db = factory.NewDbContext())
        {
            Assert.True(db.WorkActs.Single().Status == WorkActStatus.Final);
        }

        var edit = await PostFormAsync(factory, "/work-acts/edit/line", new Dictionary<string, string>
        {
            ["ActId"] = actId.ToString(),
            ["LineId"] = lineId.ToString(),
            ["DurationMinutes"] = "90",
            ["Description"] = "too late",
            ["HourlyRate"] = "100"
        });

        Assert.True(edit.Headers.TryGetValues("HX-Trigger", out var triggers));
        Assert.Contains("final", string.Join(",", triggers));

        await using var check = factory.NewDbContext();
        Assert.Equal(60, check.WorkActLines.First().DurationMinutes);
    }

    // ---- detail ----

    [Fact]
    public async Task Detail_ListsTheActsLines()
    {
        var (factory, projectId, _) = await SetupAsync();
        Assert.True((await CreateActViaPostAsync(factory, projectId)).IsSuccessStatusCode);

        Guid actId;
        await using (var seed = factory.NewDbContext())
        {
            actId = seed.WorkActs.Single().Id;
        }

        var html = await TestHtmx.GetHtmlAsync(factory.CreateClient(), $"/work-acts/detail/{actId}", "work-acts-detail");

        Assert.Contains("work 0", html);
        Assert.Contains("work 2", html);
        Assert.Contains("Draft", html);
    }

    // ---- delete ----

    [Fact]
    public async Task Delete_DraftAct_RemovesLinesAndKeepsEntries()
    {
        var (factory, projectId, _) = await SetupAsync();
        Assert.True((await CreateActViaPostAsync(factory, projectId)).IsSuccessStatusCode);

        Guid actId;
        await using (var seed = factory.NewDbContext())
        {
            actId = seed.WorkActs.Single().Id;
        }

        var response = await PostFormAsync(factory, $"/work-acts/delete/{actId}", []);
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}.");

        await using var db = factory.NewDbContext();
        Assert.Empty(db.WorkActs);
        Assert.Empty(db.WorkActLines);
        Assert.Equal(3, db.TimeTrackings.Count());
    }

    [Fact]
    public async Task Delete_FinalAct_RefusedAndKept()
    {
        var (factory, projectId, _) = await SetupAsync();

        Guid[] entryIds;
        await using (var seed = factory.NewDbContext())
        {
            entryIds = seed.TimeTrackings.Select(t => t.Id).ToArray();
        }

        var (act, _) = await CreateActWithLinesAsync(factory, projectId, entryIds, WorkActStatus.Final);

        var response = await PostFormAsync(factory, $"/work-acts/delete/{act.Id}", []);
        Assert.True(response.IsSuccessStatusCode, $"Expected a toast response, got {(int)response.StatusCode}.");
        Assert.True(response.Headers.TryGetValues("HX-Trigger", out var triggers));
        Assert.Contains("cannot be deleted", string.Join(",", triggers));

        await using var db = factory.NewDbContext();
        Assert.NotNull(db.WorkActs.FirstOrDefault());
        Assert.Equal(3, db.WorkActLines.Count());
    }

    [Fact]
    public async Task Delete_UnknownAct_ChangesNothing()
    {
        var (factory, _, _) = await SetupAsync(entryCount: 0);

        var response = await PostFormAsync(factory, $"/work-acts/delete/{Guid.NewGuid()}", []);

        // The HTMX error middleware turns the 404 into a toast response, so assert the effect instead.
        Assert.True(response.IsSuccessStatusCode, $"Expected a toast response, got {(int)response.StatusCode}.");

        await using var check = factory.NewDbContext();
        Assert.Empty(check.WorkActs);
    }

    // ---- time-tracking lock ----

    [Fact]
    public async Task EntryInAct_EditIsRejected()
    {
        var (factory, projectId, _) = await SetupAsync();
        Assert.True((await CreateActViaPostAsync(factory, projectId)).IsSuccessStatusCode);

        Guid entryId;
        await using (var seed = factory.NewDbContext())
        {
            entryId = seed.WorkActLines.OrderBy(l => l.StartedAtUtc).First().TimeTrackingId;
        }

        var response = await PostFormAsync(factory, $"/time-tracking/edit/{entryId}", new Dictionary<string, string>
        {
            ["Date"] = "2026-10-05",
            ["Start"] = "09:00",
            ["DurationMinutes"] = "30",
            ["Description"] = "changed after certification",
            ["ProjectId"] = projectId.ToString(),
            ["ProjectLabel"] = "Karma"
        }, tokenPage: "/time-tracking");

        Assert.True(response.IsSuccessStatusCode, $"Expected a toast response, got {(int)response.StatusCode}.");
        Assert.True(response.Headers.TryGetValues("HX-Trigger", out var triggers));
        Assert.Contains("work act", string.Join(",", triggers));

        await using var db = factory.NewDbContext();
        Assert.Equal("work 0", await db.TimeTrackings.AsNoTracking().Where(t => t.Id == entryId).Select(t => t.Description).FirstAsync(Ct));
    }

    [Fact]
    public async Task EntryInAct_DeleteIsRejected()
    {
        var (factory, projectId, _) = await SetupAsync();
        Assert.True((await CreateActViaPostAsync(factory, projectId)).IsSuccessStatusCode);

        Guid entryId;
        await using (var seed = factory.NewDbContext())
        {
            entryId = seed.WorkActLines.First().TimeTrackingId;
        }

        var response = await PostFormAsync(factory, $"/time-tracking/delete/{entryId}", [], tokenPage: "/time-tracking");

        Assert.True(response.IsSuccessStatusCode, $"Expected a toast response, got {(int)response.StatusCode}.");

        await using var db = factory.NewDbContext();
        Assert.NotNull(await db.TimeTrackings.AsNoTracking().FirstOrDefaultAsync(t => t.Id == entryId, Ct));
    }

    [Fact]
    public async Task EntryInAct_MoveIsRejected()
    {
        var (factory, projectId, _) = await SetupAsync();
        Assert.True((await CreateActViaPostAsync(factory, projectId)).IsSuccessStatusCode);

        Guid entryId;
        DateTime originalStart;
        await using (var seed = factory.NewDbContext())
        {
            var line = seed.WorkActLines.OrderBy(l => l.StartedAtUtc).First();
            entryId = line.TimeTrackingId;
            originalStart = line.StartedAtUtc;
        }

        var http = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/time-tracking/move/{entryId}?date=2026-11-02&time=10:00");
        request.Headers.TryAddWithoutValidation("HX-Request", "true");
        var response = await http.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode, $"Expected a toast response, got {(int)response.StatusCode}.");

        await using var db = factory.NewDbContext();
        Assert.Equal(originalStart, await db.TimeTrackings.AsNoTracking().Where(t => t.Id == entryId).Select(t => t.StartedAtUtc).FirstAsync(Ct));
    }

    [Fact]
    public async Task EntryInTwoActs_EditRejectionNamesBothActs()
    {
        var (factory, projectId, _) = await SetupAsync();
        Assert.True((await CreateActViaPostAsync(factory, projectId)).IsSuccessStatusCode);
        // A second act over a wider period: the same entries, but a distinguishable label.
        Assert.True((await CreateActViaPostAsync(factory, projectId, "2026-10-01", "2026-10-31")).IsSuccessStatusCode);

        Guid entryId;
        await using (var seed = factory.NewDbContext())
        {
            entryId = seed.WorkActLines.Select(l => l.TimeTrackingId).Distinct().First();
        }

        var response = await PostFormAsync(factory, $"/time-tracking/edit/{entryId}", new Dictionary<string, string>
        {
            ["Date"] = "2026-10-05",
            ["Start"] = "09:00",
            ["DurationMinutes"] = "30",
            ["Description"] = "changed",
            ["ProjectId"] = projectId.ToString(),
            ["ProjectLabel"] = "Karma"
        }, tokenPage: "/time-tracking");

        Assert.True(response.Headers.TryGetValues("HX-Trigger", out var triggers));
        Assert.Contains("work acts", string.Join(",", triggers));
    }

    [Fact]
    public async Task Grid_MarksLockedEntry()
    {
        var (factory, projectId, _) = await SetupAsync();
        Assert.True((await CreateActViaPostAsync(factory, projectId)).IsSuccessStatusCode);

        var html = await TestHtmx.GetHtmlAsync(
            factory.CreateClient(), "/time-tracking/list?weekStart=2026-10-05", "time-tracking-grid");

        Assert.Contains("tt-entry-card is-certified", html);
    }

    [Fact]
    public async Task EditDialog_ForLockedEntry_OmitsSaveAndDelete()
    {
        var (factory, projectId, _) = await SetupAsync();
        Assert.True((await CreateActViaPostAsync(factory, projectId)).IsSuccessStatusCode);

        Guid entryId;
        await using (var seed = factory.NewDbContext())
        {
            entryId = seed.WorkActLines.First().TimeTrackingId;
        }

        var html = await TestHtmx.GetHtmlAsync(factory.CreateClient(), $"/time-tracking/edit/{entryId}", "time-tracking-dialog");

        Assert.Contains("Certified by a work act", html);
        Assert.DoesNotContain(">Save</button>", html);
        Assert.DoesNotContain(">Delete</button>", html);
    }

    // ---- access ----

    [Fact]
    public async Task WorkActsRoutes_AnonymousUser_IsSentToLogin()
    {
        var (factory, _, _) = await SetupAsync();
        factory.SetAnonymous();

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/work-acts");

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, $"Expected a redirect, got {(int)response.StatusCode}.");
        Assert.StartsWith("/account/login", response.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task WorkActsRoutes_RolelessUser_IsDenied()
    {
        var (factory, _, _) = await SetupAsync();
        factory.SetPrincipal("r@example.com", null);

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/work-acts");

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, $"Expected a redirect, got {(int)response.StatusCode}.");
        // An authenticated user without the operational role lands on the denied page, not the pending one.
        Assert.Contains("/account/denied", response.Headers.Location!.ToString());
    }
}
