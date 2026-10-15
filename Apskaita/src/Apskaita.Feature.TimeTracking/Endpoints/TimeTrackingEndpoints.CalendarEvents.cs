using Apskaita.Entities.Contracts;
using Apskaita.Feature.TimeTracking.Slices;
using Apskaita.Shared.Calendar;
using Apskaita.Shared.Routing;
using Microsoft.EntityFrameworkCore;
using TimeTrackingEntry = Apskaita.Entities.TimeTracking;
using TimeTrackingModel = Apskaita.Feature.TimeTracking.Models.TimeTrackingModel;

namespace Apskaita.Feature.TimeTracking.Endpoints;

public sealed partial class TimeTrackingEndpoints
{
    /// <summary>
    /// Calendar-event entry point (REQ-001): one URI for every event click. The server decides which
    /// dialog to return — the existing entry's edit dialog when an entry references the event, otherwise
    /// a create dialog prefilled with the event's display-zone start, duration, and subject (description).
    /// </summary>
    private sealed class CalendarEventSection(TimeTrackingEndpoints owner)
    {
        public void Map(RouteGroupBuilder group)
        {
            group.MapGet("event", async (
                IFeatureUri uri,
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

                var trackedId = await FindTrackedEntryIdAsync(entries, reference, ct);
                if (trackedId is not null)
                {
                    // Reuse the edit route (route id is authoritative for the dialog's targets).
                    return Results.Redirect(uri.Uri($"edit/{trackedId.Value}"));
                }

                var model = TimeTrackingModel.CreateFromEvent(date, time, duration, weekStart, reference.ProviderKey, reference.AccountKey, reference.ExternalEventId, subject);
                return owner.Result.ShowDialog(_CreateDialog.Create(model));
            });
        }
    }

    /// <summary>
    /// Id of the existing time entry referencing the event triple (unique index ⇒ at most one), or null.
    /// </summary>
    private static async Task<Guid?> FindTrackedEntryIdAsync(
        IQuery<TimeTrackingEntry> entries,
        CalendarEventReference reference,
        CancellationToken ct)
    {
        return await entries.All()
            .Where(t => t.CalendarEvent != null
                && t.CalendarEvent!.CalendarProvider == reference.CalendarProvider
                && t.CalendarEvent.AccountKey == reference.AccountKey
                && t.CalendarEvent.ExternalEventId == reference.ExternalEventId)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(ct);
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
