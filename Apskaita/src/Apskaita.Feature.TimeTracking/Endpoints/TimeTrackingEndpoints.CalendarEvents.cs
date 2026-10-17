using Apskaita.Entities.Contracts;
using Apskaita.Feature.TimeTracking.Slices;
using Apskaita.Shared.Calendar;
using TimeTrackingEntry = Apskaita.Entities.TimeTracking;
using TimeTrackingModel = Apskaita.Feature.TimeTracking.Models.TimeTrackingModel;

namespace Apskaita.Feature.TimeTracking.Endpoints;

public sealed partial class TimeTrackingEndpoints
{
    /// <summary>
    /// Calendar-event entry point (REQ-001): one URI for every event click. The server decides the response
    /// — when no entry anywhere references the event it returns a create dialog prefilled with the event's
    /// display-zone start, duration, and subject (description); when an entry anywhere already references it
    /// it returns an empty response plus an "already logged" toast (the global unique index makes a
    /// duplicate create impossible, so no dialog is opened). The reference is validated before either.
    /// </summary>
    private sealed class CalendarEventSection(TimeTrackingEndpoints owner)
    {
        public void Map(RouteGroupBuilder group)
        {
            group.MapGet("event", async (
                string provider,
                string account,
                string eventId,
                string? date,
                string? time,
                int? duration,
                string? weekStart,
                string? subject,
                IQuery<TimeTrackingEntry> entries,
                ICalendarEventReferenceResolver eventResolver,
                CancellationToken ct) =>
            {
                var reference = await ValidateEventReferenceAsync(eventResolver, provider, account, eventId, ct);
                if (reference is null)
                {
                    return Results.NotFound();
                }

                // Global check (no week filter): the unique index is the backstop, so a create would be
                // doomed — clear the dialog host and toast instead of opening a form.
                if (await FindTrackedEntryIdAsync(entries, reference, ct) is not null)
                {
                    return owner.Result.Fragment(_Empty.Create())
                        .WithTrigger("time-tracking-list-refresh")
                        .WithToast("This calendar event is already logged.", title: "Time entry", tone: ToastTone.Warning);
                }

                var model = TimeTrackingModel.CreateFromEvent(date, time, duration, weekStart, reference.ProviderKey, reference.AccountKey, reference.ExternalEventId, subject);
                return owner.Result.ShowDialog(_CreateDialog.Create(model));
            });
        }
    }

    /// <summary>
    /// Id of the existing time entry referencing the event triple (unique index ⇒ at most one), or null.
    /// Reads through the <see cref="IQuery{TEntity}"/> seam so this file needs no database-provider reference.
    /// </summary>
    private static async Task<Guid?> FindTrackedEntryIdAsync(
        IQuery<TimeTrackingEntry> entries,
        CalendarEventReference reference,
        CancellationToken ct)
    {
        var entry = await entries.FirstOrDefaultAsync(
            t => t.CalendarEvent != null
                && t.CalendarEvent.CalendarProvider == reference.CalendarProvider
                && t.CalendarEvent.AccountKey == reference.AccountKey
                && t.CalendarEvent.ExternalEventId == reference.ExternalEventId,
            ct);

        return entry?.Id;
    }

    /// <summary>
    /// Validates a calendar event reference triple (REQ-004): provider key parses case-insensitively,
    /// keys are non-blank and within <see cref="CalendarEventReference.MaxKeyLength"/>, and the account
    /// belongs to one of the current user's connectors for that provider (via Account's resolver).
    /// Null when invalid.
    /// </summary>
    private static async Task<CalendarEventReference?> ValidateEventReferenceAsync(
        ICalendarEventReferenceResolver eventResolver,
        string? providerKey,
        string? accountKey,
        string? externalEventId,
        CancellationToken ct)
    {
        if (!CalendarEventReference.TryCreate(providerKey, accountKey, externalEventId, out var reference) || reference is null)
        {
            return null;
        }

        return await eventResolver.OwnsAccountAsync(reference.CalendarProvider, reference.AccountKey, ct) ? reference : null;
    }
}
