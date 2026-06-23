using RazorSlicesHtmx.FluentValidation.Models;

namespace RazorSlicesHtmx.Bootstrap5.Extensions;

public static class BootstrapValidationResultViewExtensions
{
    public static string InputClass(
        this IHaveValidationResult source,
        string key,
        string baseClass = "form-control")
    {
        return source.Errors.ContainsKey(key)
            ? $"{baseClass} is-invalid"
            : baseClass;
    }
}