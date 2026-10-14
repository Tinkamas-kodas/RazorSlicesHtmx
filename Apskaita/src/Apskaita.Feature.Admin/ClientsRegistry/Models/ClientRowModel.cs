namespace Apskaita.Feature.Admin.ClientsRegistry.Models;

public sealed class ClientRowModel
{
    [SortDisable] public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string? CompanyCode { get; init; }
    public string? VatCode { get; init; }
    public string? ContactEmail { get; init; }
    public bool IsActive { get; init; }
}
