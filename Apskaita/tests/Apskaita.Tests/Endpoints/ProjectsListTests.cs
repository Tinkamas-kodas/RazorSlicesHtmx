using Apskaita.Entities;

namespace Apskaita.Tests.Endpoints;

/// <summary>
/// Runtime smoke for the projects list endpoint: the row projection composes across the feature
/// line through <c>IQuery&lt;Project&gt;</c> and joins to Client/InvoiceTemplate for the display
/// columns. Verifies the full-page and HTMX fragment variants plus the search filter over the
/// projected rows (closes the plan caveat that the nav-projection SQL was compile-verified only).
/// </summary>
[Collection("Integration")] // in-process hosts must not run concurrently (ApskaitaApiFactory race)
public sealed class ProjectsListTests
{
    private static void SeedProject(ApskaitaApiFactory factory, string projectName, string clientName, string templateName)
    {
        var projectId = Guid.NewGuid();
        using var db = factory.NewDbContext();

        var client = new Client { Id = Guid.NewGuid(), Info = new CompanyInfo { Name = clientName } };
        var template = new InvoiceTemplate
        {
            Id = Guid.NewGuid(),
            Code = "TPL",
            Name = templateName,
            LanguageCode = "en",
            Content = "x",
            IsActive = true
        };
        db.Clients.Add(client);
        db.InvoiceTemplates.Add(template);
        db.Projects.Add(new Project
        {
            Id = projectId,
            ClientId = client.Id,
            InvoiceTemplateId = template.Id,
            Name = projectName,
            Code = "PRJ",
            HourlyRate = 100,
            CurrencyCode = "EUR",
            IsActive = true
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task List_FullPage_RendersJoinedRowColumns()
    {
        await using var factory = new ApskaitaApiFactory();
        SeedProject(factory, "Smoke Project", "Acme", "Standard");

        var http = factory.CreateClient();
        var response = await http.GetAsync("/admin/projects/list");

        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}.");
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Smoke Project", content);
        Assert.Contains("Acme", content);
        Assert.Contains("Standard", content);
        Assert.Contains("projects-list", content);
        Assert.Contains("<!DOCTYPE html>", content);
    }

    [Fact]
    public async Task List_HtmxRequest_ReturnsFragmentOnly()
    {
        await using var factory = new ApskaitaApiFactory();
        SeedProject(factory, "Smoke Project", "Acme", "Standard");

        var http = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/projects/list");
        request.Headers.TryAddWithoutValidation("HX-Request", "true");
        var response = await http.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}.");
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Smoke Project", content);
        Assert.Contains("projects-list", content);
        Assert.DoesNotContain("<!DOCTYPE html>", content);
    }

    [Fact]
    public async Task List_WithSearch_FiltersProjectedRows()
    {
        await using var factory = new ApskaitaApiFactory();
        SeedProject(factory, "Smoke Project", "Acme", "Standard");
        SeedProject(factory, "Other Project", "Globex", "Rush");

        var http = factory.CreateClient();
        var content = await http.GetStringAsync("/admin/projects/list?Search=Smoke");

        Assert.Contains("Smoke Project", content);
        Assert.DoesNotContain("Other Project", content);
    }
}
