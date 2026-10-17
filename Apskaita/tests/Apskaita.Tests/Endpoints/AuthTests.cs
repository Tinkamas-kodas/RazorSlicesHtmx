using System.Net;
using System.Text.RegularExpressions;
using Apskaita.Entities;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Apskaita.Tests.Endpoints;

/// <summary>
/// Integration tests for the authentication/authorization stack: anonymous redirects, login page,
/// provider challenges, HTMX-aware login redirect, role-based access, roleless pending landing, and
/// admin role assignment. Uses the stub authentication principal from <see cref="ApskaitaApiFactory"/>.
/// </summary>
[Collection("Integration")] // in-process hosts must not run concurrently (ApskaitaApiFactory race)
public sealed class AuthTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static HttpClient NoRedirectClient(ApskaitaApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Unauthenticated_GetRoot_RedirectsToLogin()
    {
        await using var factory = new ApskaitaApiFactory();
        factory.SetAnonymous();

        var response = await NoRedirectClient(factory).GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.AbsolutePath;
        Assert.StartsWith("/account/login", location);
    }

    [Fact]
    public async Task LoginPage_WithoutConfiguredProviders_OffersNeither()
    {
        await using var factory = new ApskaitaApiFactory
        {
            ConfigOverrides =
            {
                ["Auth:Entra:ClientId"] = string.Empty,
                ["Auth:Google:ClientId"] = string.Empty
            }
        };
        factory.SetAnonymous();

        // Both providers forced unconfigured (overrides dev user-secrets), so neither button is rendered.
        var html = await factory.CreateClient().GetStringAsync("/account/login");

        Assert.DoesNotContain("/account/challenge/entra", html);
        Assert.DoesNotContain("/account/challenge/google", html);
        Assert.Contains("No sign-in providers", html);
    }

    [Fact]
    public async Task LoginPage_WithConfiguredProviders_OffersBoth()
    {
        await using var factory = new ApskaitaApiFactory
        {
            ConfigOverrides =
            {
                ["Auth:Entra:ClientId"] = "entra-test-client",
                ["Auth:Google:ClientId"] = "google-test-client"
            }
        };
        factory.SetAnonymous();

        var html = await factory.CreateClient().GetStringAsync("/account/login");

        Assert.Contains("/account/challenge/entra", html);
        Assert.Contains("/account/challenge/google", html);
    }

    [Fact]
    public async Task Challenge_UnknownScheme_ReturnsBadRequest()
    {
        await using var factory = new ApskaitaApiFactory();
        factory.SetAnonymous();

        // Only 'entra' and 'google' are valid sign-in schemes; any other is rejected regardless of config.
        var response = await factory.CreateClient().GetAsync("/account/challenge/facebook?returnUrl=/");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HtmxUnauthenticated_Request_GetsHxRedirectInsteadOfLoginPage()
    {
        await using var factory = new ApskaitaApiFactory();
        factory.SetAnonymous();

        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/clients/list");
        request.Headers.TryAddWithoutValidation("HX-Request", "true");

        var response = await NoRedirectClient(factory).SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("HX-Redirect", out var values));
        Assert.Contains("/account/login", string.Join(",", values));
    }

    [Fact]
    public async Task RolelessUser_GetRoot_RedirectsToPending()
    {
        await using var factory = new ApskaitaApiFactory();
        factory.SetPrincipal("r@example.com", null);

        var response = await NoRedirectClient(factory).GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/account/pending", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task UserRole_CanAccessOperational_ButNotAdmin()
    {
        await using var factory = new ApskaitaApiFactory();
        factory.SetPrincipal("u@example.com", UserRole.User);

        var http = factory.CreateClient();

        var operational = await http.GetAsync("/time-tracking");
        Assert.True(operational.IsSuccessStatusCode, $"Operational feature should be accessible, got {(int)operational.StatusCode}.");

        var admin = await NoRedirectClient(factory).GetAsync("/admin/clients/list");
        Assert.Equal(HttpStatusCode.Redirect, admin.StatusCode);
        Assert.Equal("/account/denied", admin.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task Admin_CanAccessRegistries()
    {
        await using var factory = new ApskaitaApiFactory();

        var response = await factory.CreateClient().GetAsync("/admin/clients/list");

        Assert.True(response.IsSuccessStatusCode, $"Admin should access registries, got {(int)response.StatusCode}.");
    }

    [Fact]
    public async Task SetRole_AdminAssignsRole_PersistsAndUpdatesBadge()
    {
        await using var factory = new ApskaitaApiFactory();

        Guid userId;
        await using (var db = factory.NewDbContext())
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "pending@example.com",
                DisplayName = "Pending",
                Provider = "entra",
                SubjectId = "oid-1",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(Ct);
            userId = user.Id;
        }

        var http = factory.CreateClient();
        var listPage = await http.GetStringAsync("/admin/users/list");
        var token = Regex.Match(listPage, "request-verification-token\"\\s+content=\"([^\"]+)\"").Groups[1].Value;
        Assert.True(!string.IsNullOrEmpty(token), "Antiforgery token should be present on the list page.");

        var form = new Dictionary<string, string> { ["Role"] = "user" };
        var request = new HttpRequestMessage(HttpMethod.Post, $"/admin/users/set-role/{userId}")
        {
            Content = new FormUrlEncodedContent(form)
        };
        request.Headers.TryAddWithoutValidation("HX-Request", "true");
        request.Headers.TryAddWithoutValidation("RequestVerificationToken", token);

        var response = await http.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}.");
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains($"user-role-{userId}", content);

        await using (var db = factory.NewDbContext())
        {
            Assert.Equal(UserRole.User, db.Users.Single().Role);
        }
    }

    [Fact]
    public async Task SetRole_InvalidRole_ReturnsBadRequest()
    {
        await using var factory = new ApskaitaApiFactory();

        Guid userId;
        await using (var db = factory.NewDbContext())
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "target@example.com",
                DisplayName = "Target",
                Provider = "entra",
                SubjectId = "oid-2",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(Ct);
            userId = user.Id;
        }

        var http = factory.CreateClient();
        var listPage = await http.GetStringAsync("/admin/users/list");
        var token = Regex.Match(listPage, "request-verification-token\"\\s+content=\"([^\"]+)\"").Groups[1].Value;

        var form = new Dictionary<string, string> { ["Role"] = "superuser" };
        var request = new HttpRequestMessage(HttpMethod.Post, $"/admin/users/set-role/{userId}")
        {
            Content = new FormUrlEncodedContent(form)
        };
        request.Headers.TryAddWithoutValidation("RequestVerificationToken", token);

        var response = await http.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var db2 = factory.NewDbContext();
        Assert.Null(db2.Users.Single().Role);
    }
}
