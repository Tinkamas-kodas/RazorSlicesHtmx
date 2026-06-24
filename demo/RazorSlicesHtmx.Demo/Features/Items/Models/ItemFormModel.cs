namespace RazorSlicesHtmx.Demo.Features.Items.Models;

public sealed record ItemFormModel(
    string Title,
    string SubmitLabel,
    string PostUrl,
    string CancelUrl,
    ItemUpsertRequest Request,
    IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> Errors) : IHaveValidationResult;