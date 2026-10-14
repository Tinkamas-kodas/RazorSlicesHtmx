namespace Apskaita.Feature.Admin.SystemConfig.Models;

[GenerateHtmlNames]
public sealed partial class SystemSettingsUpsertRequest : IHaveValidationResult
{
    public int Id { get; set; }
    public decimal DefaultVatRatePercent { get; set; }
    public string? InvoiceNumberTemplate { get; set; }

    public IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> Errors { get; set; } =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();

    private static readonly IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> EmptyErrors =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();

    public static SystemSettingsUpsertRequest CreateDefault() => new()
    {
        Id = 1,
        DefaultVatRatePercent = 21,
        InvoiceNumberTemplate = "INV-{yyyy}-{seq:0000}",
        Errors = EmptyErrors
    };

    public static SystemSettingsUpsertRequest FromEntity(SystemSettings row) => new()
    {
        Id = row.Id,
        DefaultVatRatePercent = row.DefaultVatRatePercent,
        InvoiceNumberTemplate = row.InvoiceNumberTemplate,
        Errors = EmptyErrors
    };

    public void Normalize()
    {
        InvoiceNumberTemplate = InvoiceNumberTemplate?.Trim();
    }
}

