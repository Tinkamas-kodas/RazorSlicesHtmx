namespace Apskaita.Feature.WorkActs.Models;

/// <summary>
/// One act's detail view: its header and its lines, all read-only. <see cref="IsDraft"/> enables the
/// edit action; <see cref="StatusLabel"/> already reflects the derived <c>Invoiced</c> state.
/// </summary>
public sealed record WorkActDetailModel(
    Guid Id,
    string ProjectLabel,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal TotalHours,
    decimal TotalAmount,
    string CurrencyCode,
    string StatusLabel,
    bool IsDraft,
    DateTime CreatedAtUtc,
    IReadOnlyList<WorkActLineRowModel> Lines);
