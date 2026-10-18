using Apskaita.Shared;

namespace Apskaita.Feature.WorkActs.Endpoints;

/// <summary>
/// Work-acts feature module: one route group (<c>/work-acts</c>) and one navigation item, with the routes
/// registered by its sections — <see cref="ListSection"/> (page + list fragment),
/// <see cref="CreateSection"/> (dialog, review screen with draft lines, the create itself),
/// <see cref="EditSection"/> (Draft act line edits and the Final action) and
/// <see cref="DetailSection"/> (act detail and deletion).
/// <para>
/// Reads compose over <see cref="Apskaita.Entities.Contracts.IQuery{TEntity}"/>; the act and its lines are
/// written through <see cref="Persistence.IWorkActStore"/>. The referenced time entries belong to the
/// time-tracking feature and are only ever read here — a line edit never touches an entry.
/// </para>
/// </summary>
public sealed partial class WorkActEndpoints(FeatureResultBuilder resultBuilder, ILogger<WorkActEndpoints> logger) : BaseFeatureModule(resultBuilder)
{
    public override IReadOnlyList<AdminNavItem> NavigationItems =>
    [
        // The page lives at the group root ("/work-acts"); "list" is the fragment it loads.
        new("MAIN", "work-acts", "Work acts", "", Order: 3, AuthorizationPolicy: AuthPolicies.Operational)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.RequireAuthorization(AuthPolicies.Operational);

        new ListSection(this).Map(group);
        new CreateSection(this, logger).Map(group);
        new EditSection(this, logger).Map(group);
        new DetailSection(this).Map(group);
    }
}
