using Apskaita.Entities;
using Apskaita.Entities.Contracts;
using Apskaita.Feature.TimeTracking.Models;
using Apskaita.Feature.TimeTracking.Validators;
using Apskaita.Web.Data;
using FluentValidation;
using FluentValidation.Results;

namespace Apskaita.Tests.Validators;

/// <summary>
/// Validator unit tests. The validator resolves the project label through <see cref="IQuery{T}"/>
/// (an async rule), so validation runs via <c>ValidateAsync</c> against an in-memory project row.
/// </summary>
public sealed class TimeTrackingModelValidatorTests : IDisposable
{
    private static readonly Guid ProjectId = Guid.NewGuid();

    private readonly ApskaitaDbContext _db = TestDb.New();
    private readonly IValidator<TimeTrackingModel> _validator;

    public TimeTrackingModelValidatorTests()
    {
        _db.Projects.Add(new Project
        {
            Id = ProjectId,
            ClientId = Guid.NewGuid(),
            InvoiceTemplateId = Guid.NewGuid(),
            Name = "Karma",
            Code = "KR",
            ColorRgb = "#F4B6C2",
            CurrencyCode = "EUR",
            IsActive = true
        });
        _db.SaveChanges();

        _validator = new TimeTrackingModelValidator(new TestQuery<Project>(_db));
    }

    public void Dispose() => _db.Dispose();

    private static TimeTrackingModel Valid() => new()
    {
        Date = new DateOnly(2026, 10, 5),
        Start = new TimeOnly(13, 0),
        DurationMinutes = 30,
        ProjectId = ProjectId,
        Description = "worked",
        Repeat = false
    };

    private Task<ValidationResult> Validate(TimeTrackingModel model) => _validator.ValidateAsync(model);

    private static bool HasErrorFor(ValidationResult result, string property) =>
        result.Errors.Any(e => e.PropertyName == property);

    private static bool HasMessage(ValidationResult result, string substring) =>
        result.Errors.Any(e => e.ErrorMessage.Contains(substring, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public async Task Valid_Model_Passes() => Assert.True((await Validate(Valid())).IsValid);

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1441)]
    public async Task Duration_OutOfRange_Fails(int minutes)
    {
        var model = Valid();
        model.DurationMinutes = minutes;

        var result = await Validate(model);

        Assert.False(result.IsValid);
        Assert.True(HasErrorFor(result, nameof(TimeTrackingModel.DurationMinutes)));
    }

    [Fact]
    public async Task MissingProject_Fails()
    {
        var model = Valid();
        model.ProjectId = null;

        var result = await Validate(model);

        Assert.False(result.IsValid);
        Assert.True(HasMessage(result, "Project is required"));
    }

    [Fact]
    public async Task UnknownProject_Fails()
    {
        var model = Valid();
        model.ProjectId = Guid.NewGuid();

        var result = await Validate(model);

        Assert.False(result.IsValid);
        Assert.True(HasMessage(result, "Project not found"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task NonPositiveHourlyRate_Fails(int rate)
    {
        var model = Valid();
        model.HourlyRate = rate;

        Assert.False((await Validate(model)).IsValid);
    }

    [Fact]
    public async Task PositiveHourlyRate_Passes()
    {
        var model = Valid();
        model.HourlyRate = 50;

        Assert.True((await Validate(model)).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingDescription_Fails(string? description)
    {
        var model = Valid();
        model.Description = description;

        Assert.False((await Validate(model)).IsValid);
    }

    [Fact]
    public async Task NonRepeating_NullRecurrenceFields_Passes() => Assert.True((await Validate(Valid())).IsValid);

    [Fact]
    public async Task Repeating_MissingWeeksAndUntil_ReportsBothRequired()
    {
        var model = Valid();
        model.Repeat = true;
        model.RepeatEveryWeeks = null;
        model.RepeatUntil = null;

        var result = await Validate(model);

        Assert.False(result.IsValid);
        Assert.True(HasMessage(result, "Repeat interval is required"));
        Assert.True(HasMessage(result, "Repeat until date is required"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(105)]
    public async Task Repeating_WeeksOutOfRange_Fails(int weeks)
    {
        var model = Valid();
        model.Repeat = true;
        model.RepeatEveryWeeks = weeks;
        model.RepeatUntil = new DateOnly(2026, 11, 2);

        Assert.False((await Validate(model)).IsValid);
    }

    [Fact]
    public async Task Repeating_UntilNotAfterDate_Fails()
    {
        var model = Valid();
        model.Repeat = true;
        model.RepeatEveryWeeks = 1;
        model.RepeatUntil = new DateOnly(2026, 10, 5); // same day, not after

        var result = await Validate(model);

        Assert.False(result.IsValid);
        Assert.True(HasMessage(result, "after the entry date"));
    }

    [Fact]
    public async Task Repeating_SpanTooLong_Fails()
    {
        var model = Valid();
        model.Repeat = true;
        model.RepeatEveryWeeks = 1;
        model.RepeatUntil = new DateOnly(2026, 10, 5).AddDays(TimeTrackingLimits.MaxRepeatSpanDays + 1);

        var result = await Validate(model);

        Assert.False(result.IsValid);
        Assert.True(HasMessage(result, "years of the entry date"));
    }

    [Fact]
    public async Task Repeating_ValidRecurrence_Passes()
    {
        var model = Valid();
        model.Repeat = true;
        model.RepeatEveryWeeks = 2;
        model.RepeatUntil = new DateOnly(2026, 10, 5).AddDays(30);

        Assert.True((await Validate(model)).IsValid);
    }
}
