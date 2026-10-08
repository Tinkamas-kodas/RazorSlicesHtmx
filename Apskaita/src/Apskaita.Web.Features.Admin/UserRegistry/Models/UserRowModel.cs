namespace Apskaita.Web.Features.Admin.UserRegistry.Models;

public sealed class UserRowModel
{
    [SortDisable] public Guid Id { get; init; }
    public string DisplayName { get; init; } = null!;
    public string EntraObjectId { get; init; } = null!;
    public string? Email { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? LastLoginAtUtc { get; init; }
    public bool IsActive { get; init; }
}

