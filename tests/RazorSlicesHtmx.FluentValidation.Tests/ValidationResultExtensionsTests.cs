using FluentValidation.Results;
using RazorSlicesHtmx.FluentValidation.Extensions;
using RazorSlicesHtmx.FluentValidation.Models;

namespace RazorSlicesHtmx.FluentValidation.Tests;

public class ValidationResultExtensionsTests
{
    [Fact]
    public void ToErrorDictionary_groups_errors_by_property_name()
    {
        var validationResult = new ValidationResult(
        [
            new ValidationFailure("Code", "Code is required") { ErrorCode = "NotEmptyValidator" },
            new ValidationFailure("Code", "Code must be unique") { ErrorCode = "Unique" },
            new ValidationFailure("Name", "Name is required") { ErrorCode = "NotEmptyValidator" }
        ]);

        var errors = validationResult.ToErrorDictionary();

        Assert.Equal(2, errors.Count);
        Assert.Equal(2, errors["Code"].Length);
        Assert.Equal("NotEmptyValidator", errors["Code"][0].errorCode);
        Assert.Equal("Code is required", errors["Code"][0].errorMessage);
        Assert.Equal("Unique", errors["Code"][1].errorCode);
        Assert.Equal("Name is required", errors["Name"][0].errorMessage);
    }

    [Fact]
    public void View_extensions_read_first_error_information()
    {
        var model = new FakeValidationResult(new Dictionary<string, (string errorCode, string errorMessage)[]>
        {
            ["Code"] =
            [
                ("NotEmptyValidator", "Code is required"),
                ("Unique", "Code must be unique")
            ]
        });

        Assert.True(model.HasError("Code"));
        Assert.False(model.HasError("Name"));
        Assert.Equal("Code is required", model.FirstError("Code"));
        Assert.Equal("NotEmptyValidator", model.FirstErrorCode("Code"));
        Assert.Null(model.FirstError("Name"));
    }

    private sealed record FakeValidationResult(
        IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> Errors) : IHaveValidationResult;
}
