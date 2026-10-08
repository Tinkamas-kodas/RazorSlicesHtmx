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
    }
}
