namespace Apskaita.Web.Features.Admin.ProjectRegistry.Models;

public sealed class ProjectRowModel
{
    [SortDisable] public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string? Code { get; init; }
    public string ClientName { get; init; } = null!;
    public int HourlyRate { get; init; }
    public string CurrencyCode { get; init; } = null!;
    public string TemplateName { get; init; } = null!;
    public bool IsActive { get; init; }
}

