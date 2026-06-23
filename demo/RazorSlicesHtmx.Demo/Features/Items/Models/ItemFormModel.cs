using razr_slices_htmx2.Shared.Models;

namespace razr_slices_htmx2.Features.Items.Models;

public sealed record ItemFormModel(
    string Title,
    string SubmitLabel,
    string PostUrl,
    string CancelUrl,
    ItemUpsertRequest Request,
    IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> Errors) : IHaveValidationResult;