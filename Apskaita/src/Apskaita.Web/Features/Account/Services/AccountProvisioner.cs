using Apskaita.Entities;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Features.Account.Services;

/// <summary>
/// Outcome of provisioning a user during sign-in: whether access is allowed and the resulting role.
/// A sign-in for a deactivated user yields <see cref="Allowed"/> = false (no session is issued).
/// </summary>
public sealed record ProvisionResult(bool Allowed, UserRole? Role);

/// <summary>
/// Creates/updates the <see cref="User"/> row for an external sign-in and resolves its role.
/// The provider-asserted email (trimmed, lowercased) is the unique identity key; the provider's subject id
/// (<c>oid</c>/<c>sub</c>) is stored for audit only. First sign-in with an unknown email auto-provisions a
/// roleless user unless the email is a configured bootstrap admin (<c>Auth:AdminEmails</c>).
/// </summary>
public sealed class AccountProvisioner(ApskaitaDbContext db, IOptions<AuthOptions> options)
{
    public async Task<ProvisionResult> ProvisionAsync(
        string email,
        string displayName,
        string provider,
        string subjectId,
        CancellationToken ct)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;
        var isBootstrapAdmin = options.Value.AdminEmails
            .Any(a => string.Equals(a.Trim(), normalizedEmail, StringComparison.OrdinalIgnoreCase));

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (user is null)
        {
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedEmail : displayName.Trim(),
                Provider = provider,
                SubjectId = subjectId,
                IsActive = true,
                Role = isBootstrapAdmin ? UserRole.Admin : null,
                CreatedAtUtc = now,
                LastLoginAtUtc = now
            });

            await db.SaveChangesAsync(ct);
            return new ProvisionResult(true, isBootstrapAdmin ? UserRole.Admin : null);
        }

        if (!user.IsActive)
        {
            return new ProvisionResult(false, null);
        }

        user.DisplayName = string.IsNullOrWhiteSpace(displayName) ? user.DisplayName : displayName.Trim();
        user.Provider = provider;
        user.SubjectId = subjectId;
        user.LastLoginAtUtc = now;

        if (user.Role is null && isBootstrapAdmin)
        {
            user.Role = UserRole.Admin;
        }

        await db.SaveChangesAsync(ct);
        return new ProvisionResult(true, user.Role);
    }
}
