using FluentValidation;
using TimeTrackingModel = Apskaita.Web.Features.TimeTracking.Models.TimeTrackingModel;

namespace Apskaita.Web.Features.TimeTracking.Validators;

public sealed class TimeTrackingModelValidator : AbstractValidator<TimeTrackingModel>
{
    public TimeTrackingModelValidator()
    {
        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(1, 1440)
            .WithMessage("Duration must be between 1 and 1440 minutes.");

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
            .InclusiveBetween(1, 104)
            .When(x => x.Repeat && x.RepeatEveryWeeks.HasValue)
            .WithMessage("Repeat interval must be between 1 and 104 weeks.");

        RuleFor(x => x.RepeatUntil)
            .NotNull()
            .When(x => x.Repeat)
            .WithMessage("Repeat until date is required.");

        RuleFor(x => x.RepeatUntil)
            .Must((model, until) => model.Date == default || until > model.Date)
            .When(x => x.Repeat && x.RepeatUntil.HasValue)
            .WithMessage("Repeat until date must be after the entry date.");

        RuleFor(x => x.RepeatUntil)
            .Must((model, until) => model.Date == default || (until!.Value.DayNumber - model.Date.DayNumber) <= 730)
            .When(x => x.Repeat && x.RepeatUntil.HasValue)
            .WithMessage("Repeat until date must be within two years of the entry date.");
    }
}
