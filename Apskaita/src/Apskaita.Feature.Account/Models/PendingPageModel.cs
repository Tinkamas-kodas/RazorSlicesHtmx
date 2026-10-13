namespace Apskaita.Web.Features.Account.Models;

/// <summary>
/// View model for the pending-access page shown to authenticated users whose role has not been assigned yet.
/// </summary>
/// <param name="DisplayName">Provider display name of the signed-in user.</param>
/// <param name="Email">Normalized identity email of the signed-in user.</param>
public sealed record PendingPageModel(string DisplayName, string Email);
