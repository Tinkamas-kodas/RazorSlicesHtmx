using System.Text.RegularExpressions;
using Apskaita.Entities;

namespace Apskaita.Tests.Endpoints;

/// <summary>
/// P4: integration tests for the admin activate/deactivate toggle endpoint. Verifies the variant-b
/// response contract (self-targeted action fragment + OOB badge + toast, no form re-render) and that
/// the persisted IsActive flag flips.
/// </summary>
public sealed class AdminToggleTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task ToggleClient_ReturnsActionFragmentAndFlipsState()
    {
        await using var factory = new ApskaitaApiFactory();

        Guid clientId;
        await using (var db = factory.NewDbContext())
        {
            var client = new Client
            {
                Id = Guid.NewGuid(),
                Info = new CompanyInfo { Name = "Acme" },
                IsActive = true
            };
            db.Clients.Add(client);
            await db.SaveChangesAsync(Ct);
            clientId = client.Id;
        }

        var http = factory.CreateClient();

        var listPage = await http.GetStringAsync("/admin/clients/list");
        var token = Regex.Match(listPage, "request-verification-token\"\\s+content=\"([^\"]+)\"").Groups[1].Value;
        Assert.True(!string.IsNullOrEmpty(token), "Antiforgery token should be present on the list page.");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/admin/clients/toggle/{clientId}");
        request.Headers.TryAddWithoutValidation("HX-Request", "true");
        request.Headers.TryAddWithoutValidation("RequestVerificationToken", token);

        var response = await http.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}.");
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("status-action-", content);
        Assert.Contains("client-status-badge", content);
        Assert.DoesNotContain("<form", content);

        Assert.True(response.Headers.TryGetValues("HX-Trigger", out var triggers));
        Assert.Contains("deactivated", string.Join(",", triggers));

        await using (var db = factory.NewDbContext())
        {
            Assert.False(db.Clients.Single().IsActive, "Persisted IsActive should flip to false.");
        }
    }

    [Fact]
    public async Task ToggleClient_Twice_RestoresActive()
    {
        await using var factory = new ApskaitaApiFactory();

        Guid clientId;
        await using (var db = factory.NewDbContext())
        {
            var client = new Client { Id = Guid.NewGuid(), Info = new CompanyInfo { Name = "Acme" }, IsActive = true };
            db.Clients.Add(client);
            await db.SaveChangesAsync(Ct);
            clientId = client.Id;
        }

        var http = factory.CreateClient();
        var listPage = await http.GetStringAsync("/admin/clients/list");
        var token = Regex.Match(listPage, "request-verification-token\"\\s+content=\"([^\"]+)\"").Groups[1].Value;

        for (var i = 0; i < 2; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/admin/clients/toggle/{clientId}");
            request.Headers.TryAddWithoutValidation("HX-Request", "true");
            request.Headers.TryAddWithoutValidation("RequestVerificationToken", token);
            await http.SendAsync(request);
        }

        await using (var db = factory.NewDbContext())
        {
            Assert.True(db.Clients.Single().IsActive, "Two toggles should restore IsActive = true.");
        }
    }
}
