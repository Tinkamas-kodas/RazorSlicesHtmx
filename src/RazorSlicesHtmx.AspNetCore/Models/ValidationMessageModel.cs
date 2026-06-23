namespace RazorSlicesHtmx.AspNetCore.Models;

public sealed record ValidationMessageModel(
    IHaveValidationResult Result,
    string FieldName);