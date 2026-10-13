using Apskaita.Entities;
using Apskaita.Web.Features.Account;
using Apskaita.Web.Features.Account.Services;

namespace Apskaita.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AccountProvisioner"/> against the EF In-Memory provider: auto-provisioning,
/// email normalization, bootstrap-admin elevation, deactivation rejection, and repeat sign-in updates.
/// </summary>
public sealed class AccountProvisionerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static AccountProvisioner CreateProvisioner(ApskaitaDbContext db, params string[] adminEmails) =>
        new(new TestAccountStore(db), Microsoft.Extensions.Options.Options.Create(new AuthOptions { AdminEmails = [.. adminEmails] }));

    [Fact]
    public async Task Provisioner_UnknownEmail_CreatesRolelessUserWithNormalizedEmail()
    {
        await using var db = TestDb.New();
        var provisioner = CreateProvisioner(db);

        var result = await provisioner.ProvisionAsync("  Ann@Example.COM ", "Ann", "google", "g-1", Ct);

        Assert.True(result.Allowed);
        Assert.Null(result.Role);

        var user = Assert.Single(db.Users.Local);
        Assert.Equal("ann@example.com", user.Email);
        Assert.Equal("google", user.Provider);
        Assert.Equal("g-1", user.SubjectId);
        Assert.Equal("Ann", user.DisplayName);
        Assert.True(user.IsActive);
        Assert.Null(user.Role);
        Assert.NotNull(user.LastLoginAtUtc);
    }

    [Fact]
    public async Task Provisioner_BootstrapEmail_GrantsAdminOnFirstSignIn()
    {
        await using var db = TestDb.New();
        var provisioner = CreateProvisioner(db, "boss@example.com");

        var result = await provisioner.ProvisionAsync("Boss@Example.com", "Boss", "entra", "oid-1", Ct);

        Assert.True(result.Allowed);
        Assert.Equal(UserRole.Admin, result.Role);
        Assert.Equal(UserRole.Admin, db.Users.Local.Single().Role);
    }

    [Fact]
    public async Task Provisioner_ExistingRolelessUser_KeepsRoleAndUpdatesProviderSubject()
    {
        await using var db = TestDb.New();
        var provisioner = CreateProvisioner(db);

        await provisioner.ProvisionAsync("a@example.com", "A", "google", "g-1", Ct);
        var result = await provisioner.ProvisionAsync("A@Example.com", "A", "entra", "oid-9", Ct);

        Assert.True(result.Allowed);
        Assert.Null(result.Role);

        var user = db.Users.Local.Single();
        Assert.Equal("entra", user.Provider);
        Assert.Equal("oid-9", user.SubjectId);
        Assert.Single(db.Users.Local);
    }

    [Fact]
    public async Task Provisioner_DeactivatedUser_IsRejected()
    {
        await using var db = TestDb.New();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "gone@example.com",
            DisplayName = "Gone",
            Provider = "entra",
            SubjectId = "oid-0",
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(Ct);

        var provisioner = CreateProvisioner(db);
        var result = await provisioner.ProvisionAsync("Gone@Example.com", "Gone", "entra", "oid-1", Ct);

        Assert.False(result.Allowed);
        Assert.Null(result.Role);
        Assert.Equal("oid-0", db.Users.Local.Single().SubjectId);
    }

    [Fact]
    public async Task Provisioner_MissingDisplayName_FallsBackToEmail()
    {
        await using var db = TestDb.New();
        var provisioner = CreateProvisioner(db);

        await provisioner.ProvisionAsync("no-name@example.com", string.Empty, "google", "g-2", Ct);

        Assert.Equal("no-name@example.com", db.Users.Local.Single().DisplayName);
    }
}
