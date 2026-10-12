using Apskaita.Entities;

namespace Apskaita.Tests.Endpoints;

/// <summary>
/// P4: integration test for the time-tracking create endpoint's weekly-recurrence batch insert.
/// The create route disables antiforgery, so no token is required.
/// </summary>
public sealed class TimeTrackingCreateTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Create_WeeklyRecurrence_BatchInsertsOccurrences()
    {
        await using var factory = new ApskaitaApiFactory();

        var projectId = Guid.NewGuid();
        await using (var db = factory.NewDbContext())
        {
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
            await db.SaveChangesAsync(Ct);
        }

        var form = new Dictionary<string, string>
        {
            ["Date"] = "2026-10-05",
            ["Start"] = "13:00",
            ["DurationMinutes"] = "30",
            ["Description"] = "worked",
            ["ProjectId"] = projectId.ToString(),
            ["ProjectLabel"] = "Karma",
            ["Repeat"] = "true",
            ["RepeatEveryWeeks"] = "1",
            ["RepeatUntil"] = "2026-10-26"
        };

        var http = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/time-tracking/create")
        {
            Content = new FormUrlEncodedContent(form)
        };
        request.Headers.TryAddWithoutValidation("HX-Request", "true");
        request.Headers.TryAddWithoutValidation("HX-Target", "dialog-host");

        var response = await http.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}.");
        Assert.True(response.Headers.TryGetValues("HX-Trigger", out var triggers));
        Assert.Contains("time-tracking-list-refresh", string.Join(",", triggers));

        await using (var db = factory.NewDbContext())
        {
            // Base entry + 3 weekly occurrences (10-05, 10-12, 10-19, 10-26) = 4 rows.
            Assert.Equal(4, db.TimeTrackings.Count());
        }
    }

    [Fact]
    public async Task Create_ValidationFailure_DoesNotInsert()
    {
        await using var factory = new ApskaitaApiFactory();

        var form = new Dictionary<string, string>
        {
            ["Date"] = "2026-10-05",
            ["Start"] = "13:00",
            ["DurationMinutes"] = "30",
            ["Description"] = "",           // fails NotEmpty
            ["ProjectLabel"] = "Karma"      // ProjectId omitted -> binds null -> fails NotNull
        };

        var http = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/time-tracking/create")
        {
            Content = new FormUrlEncodedContent(form)
        };
        request.Headers.TryAddWithoutValidation("HX-Request", "true");
        request.Headers.TryAddWithoutValidation("HX-Target", "dialog-host");

        var response = await http.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode, "Validation failure re-renders the dialog with 200.");
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("alert-danger", content);

        await using var db = factory.NewDbContext();
        Assert.Equal(0, db.TimeTrackings.Count());
    }
}
