using Apskaita.Shared;

namespace Apskaita.Feature.TimeTracking.Endpoints;

/// <summary>
/// Time-tracking feature module. One route group (<c>/time-tracking</c>) and one navigation item; the
/// routes themselves are registered by the feature's sections, each in its own file:
/// <see cref="WeekSection"/> (week grid and the prefix-derived redirect), <see cref="EntrySection"/>
/// (create/edit/delete/move), <see cref="CalendarEventSection"/> (calendar-event click entry point).
/// The dialog's project picker is served by the shared <c>/lookup/projects</c> routes.
/// <para>
/// Reads compose over <see cref="Apskaita.Entities.Contracts.IQuery{TEntity}"/> (no-tracking); writes
/// mutate through <see cref="Persistence.ITimeTrackingStore"/> (tracked). Calendar event references are
/// validated against Account's connectors via <see cref="Apskaita.Shared.Calendar.ICalendarEventReferenceResolver"/>
/// — the feature never touches another feature's tables directly.
/// </para>
/// </summary>
public sealed partial class TimeTrackingEndpoints(FeatureResultBuilder resultBuilder, ILogger<TimeTrackingEndpoints> logger) : BaseFeatureModule(resultBuilder)
{
    public override IReadOnlyList<AdminNavItem> NavigationItems =>
    [
        // The page lives at the group root ("/time-tracking"); "list" is the grid fragment it loads.
        new("MAIN", "time-tracking", "Time tracking", "", Order: 1, AuthorizationPolicy: AuthPolicies.Operational)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.RequireAuthorization(AuthPolicies.Operational);

        new WeekSection(this).Map(group);
        new EntrySection(this, logger).Map(group);
        new CalendarEventSection(this).Map(group);
    }
}
