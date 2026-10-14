namespace Apskaita.Feature.Account.UserRegistry.Models;

public sealed record UserViewModel(
    Guid Id,
    string DisplayName,
    string Email,
    string Provider,
    UserRole? Role,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc,
    bool IsActive);

