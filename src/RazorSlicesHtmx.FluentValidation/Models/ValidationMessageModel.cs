namespace RazorSlicesHtmx.FluentValidation.Models;

public sealed record ValidationMessageModel(
    IHaveValidationResult Result,
    string FieldName);