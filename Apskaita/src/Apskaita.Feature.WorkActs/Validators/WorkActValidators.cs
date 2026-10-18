using Apskaita.Entities;
using Apskaita.Entities.Contracts;
using Apskaita.Feature.WorkActs.Models;
using FluentValidation;

namespace Apskaita.Feature.WorkActs.Validators;

public sealed class WorkActCreateRequestValidator : AbstractValidator<WorkActCreateRequest>
{
    public WorkActCreateRequestValidator(IQuery<Project> projects)
    {
        RuleFor(x => x.ProjectCode)
            .NotEmpty()
            .WithMessage("Project is required.");

        RuleFor(x => x.ProjectCode)
            .MustAsync((code, cancellationToken) =>
                projects.ExistsAsync(p => p.Code == code, cancellationToken))
            .WithMessage("Project not found.")
            .When(model => !string.IsNullOrWhiteSpace(model.ProjectCode));

        RuleFor(x => x.DateFrom)
            .NotEmpty()
            .WithMessage("Start date is required.");

        RuleFor(x => x.DateTo)
            .NotEmpty()
            .WithMessage("End date is required.");

        RuleFor(x => x.DateTo)
            .GreaterThanOrEqualTo(x => x.DateFrom)
            .WithMessage("The end date must not be before the start date.")
            .When(x => x.DateFrom != default && x.DateTo != default);
    }
}

/// <summary>Field limits shared by work-act line edits.</summary>
internal static class WorkActLineRules
{
    public const int MinDurationMinutes = 1;
    public const int MaxDurationMinutes = 1440;
}

public sealed class WorkActLineEditRequestValidator : AbstractValidator<WorkActLineEditRequest>
{
    public WorkActLineEditRequestValidator()
    {
        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(WorkActLineRules.MinDurationMinutes, WorkActLineRules.MaxDurationMinutes)
            .WithMessage($"Duration must be between {WorkActLineRules.MinDurationMinutes} and {WorkActLineRules.MaxDurationMinutes} minutes.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Description is required.")
            .MaximumLength(FieldLengths.WorkActLine.Description)
            .WithMessage($"Description must be at most {FieldLengths.WorkActLine.Description} characters.");

        RuleFor(x => x.HourlyRate)
            .GreaterThan(0)
            .WithMessage("Hourly rate must be greater than 0.");
    }
}
