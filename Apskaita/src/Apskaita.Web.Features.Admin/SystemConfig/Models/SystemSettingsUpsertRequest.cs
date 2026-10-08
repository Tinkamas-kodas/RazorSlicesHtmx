namespace Apskaita.Web.Features.Admin.SystemConfig.Models;

[GenerateHtmlNames]
public sealed partial class SystemSettingsUpsertRequest : IHaveValidationResult
{
    public int Id { get; set; }
    public decimal DefaultVatRatePercent { get; set; }
    public string? InvoiceNumberTemplate { get; set; }

    public IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> Errors { get; set; } =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();
}

