namespace Apskaita.Web.Features.Admin.InvoiceTemplateRegistry.Models;

public sealed class InvoiceTemplateRowModel
{
    [SortDisable] public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string LanguageCode { get; init; } = null!;
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; }
}

