using RazorSlicesHtmx.AspNetCore.Models;

namespace RazorSlicesHtmx.AspNetCore.Extensions;

public static class ValidationResultViewExtensions
{
    public static bool HasError(this IHaveValidationResult source, string key) =>
        source.Errors.ContainsKey(key);

    public static string? FirstError(this IHaveValidationResult source, string key) =>
        source.Errors.TryGetValue(key, out var errors)
            ? errors.FirstOrDefault().errorMessage
            : null;

    public static string? FirstErrorCode(this IHaveValidationResult source, string key) =>
        source.Errors.TryGetValue(key, out var errors)
            ? errors.FirstOrDefault().errorCode
            : null;
}