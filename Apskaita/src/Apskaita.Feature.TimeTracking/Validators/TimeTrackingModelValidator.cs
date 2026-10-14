using Apskaita.Entities.Contracts;
using FluentValidation;
using Apskaita.Feature.TimeTracking.Models;
using Microsoft.EntityFrameworkCore;
using TimeTrackingModel = Apskaita.Feature.TimeTracking.Models.TimeTrackingModel;

namespace Apskaita.Feature.TimeTracking.Validators;

public static class TimeTrackingValidationContextExtensions
{
    public static void SetProjectLabel (this ValidationContext<TimeTrackingModel> context, string? label)
    {
        context.RootContextData["ProjectLabel"] = label;
    }
    public static string? GetProjectLabel(this ValidationContext<TimeTrackingModel> context)
    {
        return (string?)context.RootContextData["ProjectLabel"];
    }
}
public sealed class TimeTrackingModelValidator : AbstractValidator<TimeTrackingModel>
{
    public TimeTrackingModelValidator(IQuery<Project> projects)
    {
        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(TimeTrackingLimits.MinDurationMinutes, TimeTrackingLimits.MaxDurationMinutes)
            .WithMessage($"Duration must be between {TimeTrackingLimits.MinDurationMinutes} and {TimeTrackingLimits.MaxDurationMinutes} minutes.");

        RuleFor(x => x.ProjectId)
            .NotNull()
            .WithMessage("Project is required.");
        
        RuleFor(x=>x.ProjectId)
            .MustAsync(async (_, projectId,ctx, cancellationToken) =>
            {
                var label =await projects
                    .All()
                    .Where(p => p.Id == projectId)
                    .Select(p=>p.Code == null ? p.Name : $"{p.Name} ({p.Code})")
                    .FirstOrDefaultAsync(cancellationToken);
                
                ctx.SetProjectLabel(label);
                return label!=null;
            })
            .WithMessage("Project must exist.")
            .When(model => model.ProjectId.HasValue);

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
