namespace Apskaita.Feature.Account.Models;

/// <summary>
/// Model for the dock's "Import" dialog: which imported calendar the file replaces and the name shown in
/// the dialog title. The calendar itself already exists (the dock never creates calendars — that stays on
/// the account page). <see cref="Error"/> is set when a previous attempt was rejected, so the dialog stays
/// open with the reason.
/// </summary>
public sealed record IcsImportDialogModel(Guid Id, string Name, string? Error = null);
