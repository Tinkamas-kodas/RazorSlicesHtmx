using Apskaita.Entities;
using Apskaita.Feature.Admin.ProjectRegistry.Models;
using Apskaita.Feature.Admin.ProjectRegistry.Validators;
using Apskaita.Web.Data;
using FluentValidation;
using FluentValidation.Results;

namespace Apskaita.Tests.Validators;

/// <summary>
/// Project upsert validation: the code is required and unique across projects, with the edit case
/// excluding the row being edited.
/// </summary>
public sealed class ProjectUpsertRequestValidatorTests : IDisposable
{
    private static readonly Guid ExistingId = Guid.NewGuid();

    private readonly ApskaitaDbContext _db = TestDb.New();
    private readonly IValidator<ProjectUpsertRequest> _validator;

    public ProjectUpsertRequestValidatorTests()
    {
        _db.Projects.Add(new Project
        {
            Id = ExistingId,
            ClientId = Guid.NewGuid(),
            InvoiceTemplateId = Guid.NewGuid(),
            Name = "Karma",
            Code = "KRM",
            HourlyRate = 100,
            ColorRgb = "#F4B6C2",
            CurrencyCode = "EUR",
            IsActive = true
        });
        _db.SaveChanges();

        _validator = new ProjectUpsertRequestValidator(new TestQuery<Project>(_db));
    }

    public void Dispose() => _db.Dispose();

    private static ProjectUpsertRequest Valid() => new()
    {
        ClientId = Guid.NewGuid(),
        Name = "Zebra",
        Code = "ZBR",
        HourlyRate = 50,
        ColorRgb = "#AAAAAA",
        CurrencyCode = "EUR",
        InvoiceTemplateId = Guid.NewGuid()
    };

    private Task<ValidationResult> Validate(ProjectUpsertRequest request) => _validator.ValidateAsync(request);

    [Fact]
    public async Task ValidRequest_Passes()
    {
        var result = await Validate(Valid());
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task EmptyCode_IsRejected()
    {
        var request = Valid();
        request.Code = "  ";

        var result = await Validate(request);

        Assert.Contains(result.Errors, e => e.ErrorMessage == "Code is required.");
    }

    [Fact]
    public async Task DuplicateCode_IsRejected()
    {
        var request = Valid();
        request.Code = "KRM";

        var result = await Validate(request);

        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("already uses this code"));
    }

    [Fact]
    public async Task EditSameProject_KeepsOwnCode()
    {
        var request = Valid();
        request.Id = ExistingId;
        request.Code = "KRM";

        var result = await Validate(request);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }
}
