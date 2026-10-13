namespace Apskaita.Web.Features.Admin.IssuerRegistry.Models;

public sealed class IssuerRowModel
{
    [SortDisable] public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string? CompanyCode { get; init; }
    public string? VatCode { get; init; }
    public string? ContactEmail { get; init; }
    public bool IsActive { get; init; }
}

