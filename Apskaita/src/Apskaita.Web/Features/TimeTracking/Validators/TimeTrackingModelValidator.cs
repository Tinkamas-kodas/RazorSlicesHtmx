using FluentValidation;
using Apskaita.Web.Features.TimeTracking.Models;
using TimeTrackingModel = Apskaita.Web.Features.TimeTracking.Models.TimeTrackingModel;

namespace Apskaita.Web.Features.TimeTracking.Validators;

public sealed class TimeTrackingModelValidator : AbstractValidator<TimeTrackingModel>
{
    public TimeTrackingModelValidator()
    {
        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(TimeTrackingLimits.MinDurationMinutes, TimeTrackingLimits.MaxDurationMinutes)
            .WithMessage($"Duration must be between {TimeTrackingLimits.MinDurationMinutes} and {TimeTrackingLimits.MaxDurationMinutes} minutes.");

        RuleFor(x => x.ProjectId)
            .NotNull()
            .WithMessage("Project is required.");

        RuleFor(x => x.HourlyRate)
            .GreaterThan(0)
            .When(x => x.HourlyRate.HasValue)
            .WithMessage("Hourly rate override must be greater than 0.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Description is required.");

        RuleFor(x => x.RepeatEveryWeeks)
            .NotNull()
            .When(x => x.Repeat)
            .WithMessage("Repeat interval is required.");

        RuleFor(x => x.RepeatEveryWeeks)
            .InclusiveBetween(TimeTrackingLimits.MinRepeatWeeks, TimeTrackingLimits.MaxRepeatWeeks)
            .When(x => x.Repeat && x.RepeatEveryWeeks.HasValue)
            .WithMessage($"Repeat interval must be between {TimeTrackingLimits.MinRepeatWeeks} and {TimeTrackingLimits.MaxRepeatWeeks} weeks.");

        RuleFor(x => x.RepeatUntil)
            .NotNull()
            .When(x => x.Repeat)
            .WithMessage("Repeat until date is required.");

        RuleFor(x => x.RepeatUntil)
            .Must((model, until) => model.Date == default || until > model.Date)
            .When(x => x.Repeat && x.RepeatUntil.HasValue)
            .WithMessage("Repeat until date must be after the entry date.");

        RuleFor(x => x.RepeatUntil)
            .Must((model, until) => model.Date == default || (until!.Value.DayNumber - model.Date.DayNumber) <= TimeTrackingLimits.MaxRepeatSpanDays)
            .When(x => x.Repeat && x.RepeatUntil.HasValue)
            .WithMessage($"Repeat until date must be within {TimeTrackingLimits.MaxRepeatSpanDays / 365} years of the entry date.");
    }
}
