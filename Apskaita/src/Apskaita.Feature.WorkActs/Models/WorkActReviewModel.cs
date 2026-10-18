namespace Apskaita.Feature.WorkActs.Models;

/// <summary>
/// The review/edit screen: the act's persisted lines. Times are display-zone values; the rate is always
/// the effective one (never null).
/// </summary>
public sealed record WorkActReviewModel(
    Guid ActId,
    Guid ProjectId,
    string ProjectLabel,
    string CurrencyCode,
    DateOnly DateFrom,
    DateOnly DateTo,
    IReadOnlyList<WorkActLineRowModel> Lines,
    decimal TotalHours,
    decimal TotalAmount)
{
    public bool IsEmpty => Lines.Count == 0;
}

/// <summary>
/// One line row of the review/edit/detail screens. <see cref="OtherActs"/> names the other acts whose
/// lines reference the same time entry (the multi-act marker and its tooltip).
/// </summary>
public sealed record WorkActLineRowModel(
    Guid LineId,
    Guid EntryId,
    DateTime StartLocal,
    int DurationMinutes,
    string Description,
    int HourlyRate,
    decimal Amount,
    string CurrencyCode,
    IReadOnlyList<string> OtherActs,
    string? Error = null)
{
    public bool IsShared => OtherActs.Count > 0;

    public string SharedTooltip => "Also in: " + string.Join(", ", OtherActs);

    public decimal Hours => Math.Round(DurationMinutes / 60m, 2, MidpointRounding.AwayFromZero);

    /// <summary>Date and time as the screen shows them (display zone, one line).</summary>
    public string WhenLabel => StartLocal.ToString("yyyy-MM-dd HH:mm");
}

/// <summary>A line row of the review/edit screen (its form saves the line directly).</summary>
public sealed record WorkActLineRowViewModel(
    Guid ActId,
    WorkActLineRowModel Line,
    string? Error = null);
