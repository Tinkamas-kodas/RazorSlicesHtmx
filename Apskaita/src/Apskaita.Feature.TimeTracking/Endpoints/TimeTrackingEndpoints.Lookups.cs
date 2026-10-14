using Apskaita.Entities.Contracts;
using Apskaita.Feature.TimeTracking.Slices;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Feature.TimeTracking.Endpoints;

public sealed partial class TimeTrackingEndpoints
{
    /// <summary>
    /// Project lookup for the entry dialog: the searchable candidate list (hx-lookup items) and the
    /// echo of a selected project. Reads Admin-owned <c>projects</c> through <c>IQuery&lt;Project&gt;</c>.
    /// </summary>
    private sealed class ProjectLookupSection(TimeTrackingEndpoints owner)
    {
        public void Map(RouteGroupBuilder group)
        {
            group.MapGet("project-lookup", async (
                string? query,
                IQuery<Project> projects,
                CancellationToken ct) =>
            {
                var model = await CreateProjectLookupModelAsync(projects, query, ct);
                return owner.Result.Fragment(_ProjectLookupItems.Create(model));
            });

            group.MapGet("project-lookup-selected", async (
                string value,
                IQuery<Project> projects,
                CancellationToken ct) =>
            {
                if (!Guid.TryParse(value, out var projectId))
                {
                    return Results.NotFound();
                }

                var selected = await projects.All()
                    .Where(project => project.Id == projectId && project.IsActive)
                    .Select(project => new
                    {
                        value = project.Id.ToString(),
                        text = project.Code == null ? project.Name : $"{project.Name} ({project.Code})"
                    })
                    .FirstOrDefaultAsync(ct);

                return selected is null ? Results.NotFound() : Results.Ok(selected);
            });
        }

        private static async Task<Dictionary<Guid, string?>> CreateProjectLookupModelAsync(
            IQuery<Project> projects,
            string? query,
            CancellationToken ct)
        {
            var term = query?.Trim();

            var active = projects.All()
                .Where(project => project.IsActive);

            if (!string.IsNullOrWhiteSpace(term))
            {
                active = active.Where(project =>
                    project.Name.Contains(term) ||
                    (project.Code != null && project.Code.Contains(term)));
            }

            var items = await active
                .OrderBy(project => project.Name)
                .Take(20)
                .Select(project => new
                {
                    project.Id,
                    Label = project.Code == null ? project.Name : $"{project.Name} ({project.Code})"
                })
                .ToDictionaryAsync(x => x.Id, x => (string?)x.Label, ct);

            return items;
        }
    }
}
