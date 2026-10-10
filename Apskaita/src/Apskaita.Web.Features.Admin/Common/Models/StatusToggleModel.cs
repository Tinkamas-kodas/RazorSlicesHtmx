namespace Apskaita.Web.Features.Admin.Common.Models;

/// <summary>
/// Model for the shared activate/deactivate action component.
/// </summary>
/// <param name="Id">Identifier of the record being toggled (used for the swap target id).</param>
/// <param name="IsActive">Current active state.</param>
/// <param name="ToggleUrl">Absolute URL that performs the toggle (self-targeted swap).</param>
public sealed record StatusToggleModel(Guid Id, bool IsActive, string ToggleUrl);
