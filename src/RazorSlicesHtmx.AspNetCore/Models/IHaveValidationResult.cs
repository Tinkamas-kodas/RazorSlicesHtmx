namespace RazorSlicesHtmx.AspNetCore.Models;

public interface IHaveValidationResult
{
    IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> Errors { get; }
}