namespace Apskaita.Feature.WorkActs.Models;

/// <summary>
/// The act creation dialog's form: a project code and the local dates bounding the period. Validated by
/// <c>WorkActCreateRequestValidator</c>; on success the endpoint creates the Draft act with its lines and
/// navigates to the review page.
/// </summary>
[GenerateHtmlNames]
public sealed partial class WorkActCreateRequest : IHaveValidationResult
{
    public string? ProjectCode { get; set; }

    public string? ProjectLabel { get; set; }

    public DateOnly DateFrom { get; set; }

    public DateOnly DateTo { get; set; }

    public IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> Errors { get; set; } =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();

    /// <summary>Rebuild after a rejected submission, keeping what the user typed.</summary>
    public static WorkActCreateRequest FromRequest(
        WorkActCreateRequest request,
        IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> errors) => new()
    {
        ProjectCode = string.IsNullOrWhiteSpace(request.ProjectCode) ? null : request.ProjectCode.Trim(),
        ProjectLabel = string.IsNullOrWhiteSpace(request.ProjectLabel) ? null : request.ProjectLabel.Trim(),
        DateFrom = request.DateFrom,
        DateTo = request.DateTo,
        Errors = errors
    };

    /// <summary>Blank dialog defaulting to the previous calendar month (first to last day).</summary>
    public static WorkActCreateRequest CreateDefault()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);

        return new WorkActCreateRequest
        {
            ProjectCode = null,
            ProjectLabel = null,
            DateFrom = firstOfMonth.AddMonths(-1),
            DateTo = firstOfMonth.AddDays(-1)
        };
    }
}

/// <summary>
/// One persisted line edit on the Draft act edit (review) screen: the change lands on the work-act line,
/// never on the time entry it references.
/// </summary>
[GenerateHtmlNames]
public sealed partial class WorkActLineEditRequest : IHaveValidationResult
{
    public Guid ActId { get; set; }

    public Guid LineId { get; set; }

    public int DurationMinutes { get; set; }

    public string? Description { get; set; }

    public int HourlyRate { get; set; }

    public IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> Errors { get; set; } =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();
}
