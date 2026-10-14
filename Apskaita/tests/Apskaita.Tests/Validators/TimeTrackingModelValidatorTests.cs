using Apskaita.Feature.TimeTracking.Models;
using Apskaita.Feature.TimeTracking.Validators;
using FluentValidation;
using FluentValidation.Results;

namespace Apskaita.Tests.Validators;

public sealed class TimeTrackingModelValidatorTests
{
    private static readonly IValidator<TimeTrackingModel> Validator = new TimeTrackingModelValidator();

    private static TimeTrackingModel Valid() => new()
    {
        Date = new DateOnly(2026, 10, 5),
        Start = new TimeOnly(13, 0),
        DurationMinutes = 30,
        ProjectId = Guid.NewGuid(),
        Description = "worked",
        Repeat = false
    };

    private static ValidationResult Validate(TimeTrackingModel model) => Validator.Validate(model);

    private static bool HasErrorFor(ValidationResult result, string property) =>
        result.Errors.Any(e => e.PropertyName == property);

    private static bool HasMessage(ValidationResult result, string substring) =>
        result.Errors.Any(e => e.ErrorMessage.Contains(substring, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void Valid_Model_Passes() => Assert.True(Validate(Valid()).IsValid);

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1441)]
    public void Duration_OutOfRange_Fails(int minutes)
    {
        var model = Valid();
        model.DurationMinutes = minutes;

        var result = Validate(model);

        Assert.False(result.IsValid);
        Assert.True(HasErrorFor(result, nameof(TimeTrackingModel.DurationMinutes)));
    }

    [Fact]
    public void MissingProject_Fails()
    {
        var model = Valid();
        model.ProjectId = null;

        var result = Validate(model);

        Assert.False(result.IsValid);
        Assert.True(HasMessage(result, "Project is required"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void NonPositiveHourlyRate_Fails(int rate)
    {
        var model = Valid();
        model.HourlyRate = rate;

        Assert.False(Validate(model).IsValid);
    }

    [Fact]
    public void PositiveHourlyRate_Passes()
    {
        var model = Valid();
        model.HourlyRate = 50;

        Assert.True(Validate(model).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingDescription_Fails(string? description)
    {
        var model = Valid();
        model.Description = description;

        Assert.False(Validate(model).IsValid);
    }

    [Fact]
    public void NonRepeating_NullRecurrenceFields_Passes() => Assert.True(Validate(Valid()).IsValid);

    [Fact]
    public void Repeating_MissingWeeksAndUntil_ReportsBothRequired()
    {
        var model = Valid();
        model.Repeat = true;
        model.RepeatEveryWeeks = null;
        model.RepeatUntil = null;

        var result = Validate(model);

        Assert.False(result.IsValid);
        Assert.True(HasMessage(result, "Repeat interval is required"));
        Assert.True(HasMessage(result, "Repeat until date is required"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(105)]
    public void Repeating_WeeksOutOfRange_Fails(int weeks)
    {
        var model = Valid();
        model.Repeat = true;
        model.RepeatEveryWeeks = weeks;
        model.RepeatUntil = new DateOnly(2026, 11, 2);

        Assert.False(Validate(model).IsValid);
    }

    [Fact]
    public void Repeating_UntilNotAfterDate_Fails()
    {
        var model = Valid();
        model.Repeat = true;
        model.RepeatEveryWeeks = 1;
        model.RepeatUntil = new DateOnly(2026, 10, 5); // same day, not after

        var result = Validate(model);

        Assert.False(result.IsValid);
        Assert.True(HasMessage(result, "after the entry date"));
    }

    [Fact]
    public void Repeating_SpanTooLong_Fails()
    {
        var model = Valid();
        model.Repeat = true;
        model.RepeatEveryWeeks = 1;
        model.RepeatUntil = new DateOnly(2026, 10, 5).AddDays(TimeTrackingLimits.MaxRepeatSpanDays + 1);

        var result = Validate(model);

        Assert.False(result.IsValid);
        Assert.True(HasMessage(result, "years of the entry date"));
    }

    [Fact]
    public void Repeating_ValidRecurrence_Passes()
    {
        var model = Valid();
        model.Repeat = true;
        model.RepeatEveryWeeks = 2;
        model.RepeatUntil = new DateOnly(2026, 10, 5).AddDays(30);

        Assert.True(Validate(model).IsValid);
    }
}
