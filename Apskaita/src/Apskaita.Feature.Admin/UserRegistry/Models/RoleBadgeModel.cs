namespace Apskaita.Web.Features.Admin.UserRegistry.Models;

/// <summary>
/// Model for the role badge component shown in the users list and detail view.
/// The rendered span carries <c>id="user-role-{Id}"</c> so it can be updated out-of-band.
/// </summary>
/// <param name="Id">User identifier (used for the OOB swap target id).</param>
/// <param name="Role">Current role: <c>Admin</c>, <c>User</c>, or null (awaiting assignment).</param>
public sealed record RoleBadgeModel(Guid Id, UserRole? Role);

/// <summary>
/// Model for the set-role dialog rendered from the users list.
/// </summary>
/// <param name="Id">User identifier (used for the POST route).</param>
/// <param name="DisplayName">User display name shown in the dialog header.</param>
/// <param name="Email">Identity email shown in the dialog body.</param>
/// <param name="CurrentRole">Current role, null when none assigned yet.</param>
public sealed record SetRoleDialogModel(Guid Id, string DisplayName, string Email, UserRole? CurrentRole);
