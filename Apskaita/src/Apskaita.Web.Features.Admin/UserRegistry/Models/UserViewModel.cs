namespace Apskaita.Web.Features.Admin.UserRegistry.Models;

public sealed record UserViewModel(
    Guid Id,
    string DisplayName,
    string EntraObjectId,
    string? Email,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc,
    bool IsActive);

