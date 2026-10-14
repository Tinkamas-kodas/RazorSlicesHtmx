using System.Text.RegularExpressions;
using Apskaita.Entities;
using Apskaita.Tests.Endpoints;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Tests.Services;

/// <summary>
/// P3: content-service behaviour against the EF In-Memory provider. Locks the recently-established
/// invariants: create always yields an active record, edit (ApplyAsync) never changes IsActive
/// (the toggle action owns it), the user toggle flips state, and list search filters.
/// </summary>
[Collection("Integration")] // in-process hosts must not run concurrently (ApskaitaApiFactory race)
public sealed class RegistryContentServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    // ---- Clients (endpoint-level) ----

    [Fact]
    public async Task Clients_Create_ForcesActiveRegardlessOfRequest()
    {
        await using var factory = new ApskaitaApiFactory();
        var http = factory.CreateClient();

        var listPage = await http.GetStringAsync("/admin/clients/list");
        var token = Regex.Match(listPage, "request-verification-token\"\\s+content=\"([^\"]+)\"").Groups[1].Value;

        var form = new Dictionary<string, string> { ["Name"] = "Acme", ["IsActive"] = "false" };
        var request = new HttpRequestMessage(HttpMethod.Post, "/admin/clients/create")
        {
            Content = new FormUrlEncodedContent(form)
        };
        request.Headers.TryAddWithoutValidation("HX-Request", "true");
        request.Headers.TryAddWithoutValidation("RequestVerificationToken", token);

        var response = await http.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}.");

        await using var db = factory.NewDbContext();
        Assert.True(db.Clients.Single().IsActive, "Create must force IsActive = true regardless of the request flag.");
    }

    [Fact]
    public async Task Clients_Apply_DoesNotChangeIsActive()
    {
        await using var factory = new ApskaitaApiFactory();

        Guid clientId;
        await using (var db = factory.NewDbContext())
        {
            var client = new Client { Id = Guid.NewGuid(), Info = new CompanyInfo { Name = "Old" }, IsActive = false };
            db.Clients.Add(client);
            await db.SaveChangesAsync(Ct);
            clientId = client.Id;
        }

        var http = factory.CreateClient();
        var listPage = await http.GetStringAsync("/admin/clients/list");
        var token = Regex.Match(listPage, "request-verification-token\"\\s+content=\"([^\"]+)\"").Groups[1].Value;

        var form = new Dictionary<string, string> { ["Name"] = "Renamed", ["IsActive"] = "true" };
        var request = new HttpRequestMessage(HttpMethod.Post, $"/admin/clients/edit/{clientId}")
        {
            Content = new FormUrlEncodedContent(form)
        };
        request.Headers.TryAddWithoutValidation("HX-Request", "true");
        request.Headers.TryAddWithoutValidation("RequestVerificationToken", token);

        var response = await http.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}.");

        await using var verify = factory.NewDbContext();
        var updated = verify.Clients.Include(company => company.Info).Single();
        Assert.False(updated.IsActive, "Edit must not change IsActive (the toggle action owns it).");
        Assert.Equal("Renamed", updated.Info.Name);
    }

    // ---- Units (endpoint-level) ----

    [Fact]
    public async Task Units_Create_ForcesActiveRegardlessOfRequest()
    {
        await using var factory = new ApskaitaApiFactory();
        var http = factory.CreateClient();

        var listPage = await http.GetStringAsync("/admin/units/list");
        var token = Regex.Match(listPage, "request-verification-token\"\\s+content=\"([^\"]+)\"").Groups[1].Value;

        var form = new Dictionary<string, string> { ["Code"] = "h", ["Name"] = "hours", ["IsActive"] = "false" };
        var request = new HttpRequestMessage(HttpMethod.Post, "/admin/units/create") { Content = new FormUrlEncodedContent(form) };
        request.Headers.TryAddWithoutValidation("HX-Request", "true");
        request.Headers.TryAddWithoutValidation("RequestVerificationToken", token);

        var response = await http.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}.");

        await using var db = factory.NewDbContext();
        Assert.True(db.Units.Single().IsActive, "Create must force IsActive = true regardless of the request flag.");
    }

    [Fact]
    public async Task Units_Apply_DoesNotChangeIsActive()
    {
        await using var factory = new ApskaitaApiFactory();

        Guid unitId;
        await using (var db = factory.NewDbContext())
        {
            var unit = new Unit { Id = Guid.NewGuid(), Code = "h", Name = "hours", IsActive = false };
            db.Units.Add(unit);
            await db.SaveChangesAsync(Ct);
            unitId = unit.Id;
        }

        var http = factory.CreateClient();
        var listPage = await http.GetStringAsync("/admin/units/list");
        var token = Regex.Match(listPage, "request-verification-token\"\\s+content=\"([^\"]+)\"").Groups[1].Value;

        var form = new Dictionary<string, string> { ["Code"] = "h", ["Name"] = "hours edited", ["IsActive"] = "true" };
        var request = new HttpRequestMessage(HttpMethod.Post, $"/admin/units/edit/{unitId}") { Content = new FormUrlEncodedContent(form) };
        request.Headers.TryAddWithoutValidation("HX-Request", "true");
        request.Headers.TryAddWithoutValidation("RequestVerificationToken", token);

        var response = await http.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}.");

        await using var verify = factory.NewDbContext();
        var updated = verify.Units.Single();
        Assert.False(updated.IsActive, "Edit must not change IsActive (the toggle action owns it).");
        Assert.Equal("hours edited", updated.Name);
    }

    [Fact]
    public async Task Units_List_SearchFiltersByName()
    {
        await using var factory = new ApskaitaApiFactory();
        await using (var db = factory.NewDbContext())
        {
            db.Units.Add(new Unit { Id = Guid.NewGuid(), Code = "aa", Name = "Alpha", IsActive = true });
            db.Units.Add(new Unit { Id = Guid.NewGuid(), Code = "bb", Name = "Beta", IsActive = true });
            await db.SaveChangesAsync(Ct);
        }

        var http = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/units/list?search=Beta");
        request.Headers.TryAddWithoutValidation("HX-Request", "true");
        request.Headers.TryAddWithoutValidation("HX-Target", "units-list");

        var response = await http.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}.");
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Beta", content);
        Assert.DoesNotContain("Alpha", content);
    }

    // ---- Users (endpoint-level) ----

    [Fact]
    public async Task Users_ToggleActive_FlipsState()
    {
        await using var factory = new ApskaitaApiFactory();

        Guid userId;
        await using (var db = factory.NewDbContext())
        {
            var user = new User { Id = Guid.NewGuid(), DisplayName = "Test", Email = "test@example.com", Provider = "entra", SubjectId = "oid", IsActive = true };
            db.Set<User>().Add(user);
            await db.SaveChangesAsync(Ct);
            userId = user.Id;
        }

        var http = factory.CreateClient();
        var listPage = await http.GetStringAsync("/admin/users/list");
        var token = Regex.Match(listPage, "request-verification-token\"\\s+content=\"([^\"]+)\"").Groups[1].Value;

        var request = new HttpRequestMessage(HttpMethod.Post, $"/admin/users/toggle/{userId}");
        request.Headers.TryAddWithoutValidation("HX-Request", "true");
        request.Headers.TryAddWithoutValidation("RequestVerificationToken", token);

        var response = await http.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}.");

        await using (var verify = factory.NewDbContext())
        {
            Assert.False(verify.Users.Single().IsActive, "First toggle should deactivate.");
        }

        var second = new HttpRequestMessage(HttpMethod.Post, $"/admin/users/toggle/{userId}");
        second.Headers.TryAddWithoutValidation("HX-Request", "true");
        second.Headers.TryAddWithoutValidation("RequestVerificationToken", token);

        response = await http.SendAsync(second);
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}.");

        await using (var verify = factory.NewDbContext())
        {
            Assert.True(verify.Users.Single().IsActive, "Second toggle should restore active.");
        }
    }
}
