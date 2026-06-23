using FluentValidation.Results;

namespace RazorSlicesHtmx.FluentValidation.Extensions;

public static class FluentValidationResultExtensions
{
    public static IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> ToErrorDictionary(
        this ValidationResult validationResult)
    {
        return validationResult.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(error => (error.ErrorCode, error.ErrorMessage))
                    .ToArray());
    }
}