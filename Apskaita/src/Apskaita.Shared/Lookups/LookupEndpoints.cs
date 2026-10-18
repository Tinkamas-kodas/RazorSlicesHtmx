using Apskaita.Entities;
using Apskaita.Entities.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Results;

namespace Apskaita.Shared.Lookups;

/// <summary>
/// Shared entity lookups for searchable pickers: one route group (<c>/lookup</c>) with a paged search
/// endpoint and a selected-echo endpoint for projects and clients. Hosted here (not in a feature module)
/// because the picker UI is cross-feature: any form can point a <c>LookupSelect</c> at these absolute
/// paths. Reads go through <see cref="IQuery{TEntity}"/> only — Shared never writes, so the vertical-slice
/// rule holds. Labels come from <see cref="LookupLabels"/>, the single source for both the search results
/// and the echo.
/// </summary>
public static class LookupEndpoints
{
    public static IEndpointRouteBuilder MapLookupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(LookupRoutes.Group);
        group.RequireAuthorization(AuthPolicies.Operational);

        group.MapGet("projects", async (
            [AsParameters] LookupRequest request,
            IQuery<Project> projects,
            CancellationToken ct) =>
        {
            request.Normalize();

            var query = projects.All().Where(p => p.IsActive);
            if (request.Query is not null)
            {
                query = query.Where(p => p.Name.Contains(request.Query) || p.Code.Contains(request.Query));
            }

            var rows = await query
                .OrderBy(p => p.Name)
                .ThenBy(p => p.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize + 1)
                .Select(p => new { p.Id, p.Name, p.Code })
                .ToListAsync(ct);

            return Fragment(request, Page(rows, request.PageSize, r => new LookupItem(
                r.Id.ToString(),
                LookupLabels.Project(r.Code, r.Name))));
        });

        group.MapGet("projects/selected", async (
            string value,
            IQuery<Project> projects,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(value, out var id))
            {
                return Results.NotFound();
            }

            var row = await projects.All()
                .Where(p => p.Id == id && p.IsActive)
                .Select(p => new { p.Name, p.Code })
                .FirstOrDefaultAsync(ct);

            return row is null
                ? Results.NotFound()
                : Results.Ok(new { value = id.ToString(), text = LookupLabels.Project(row.Code, row.Name) });
        });

        group.MapGet("clients", async (
            [AsParameters] LookupRequest request,
            IQuery<Client> clients,
            CancellationToken ct) =>
        {
            request.Normalize();

            var query = clients.All().Where(c => c.IsActive);
            if (request.Query is not null)
            {
                query = query.Where(c => c.Info.Name.Contains(request.Query) || c.Info.CompanyCode.Contains(request.Query));
            }

            var rows = await query
                .OrderBy(c => c.Info.Name)
                .ThenBy(c => c.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize + 1)
                .Select(c => new { c.Id, c.Info.Name, c.Info.CompanyCode })
                .ToListAsync(ct);

            return Fragment(request, Page(rows, request.PageSize, r => new LookupItem(
                r.Id.ToString(),
                LookupLabels.Client(r.Name, r.CompanyCode))));
        });

        group.MapGet("clients/selected", async (
            string value,
            IQuery<Client> clients,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(value, out var id))
            {
                return Results.NotFound();
            }

            var row = await clients.All()
                .Where(c => c.Id == id && c.IsActive)
                .Select(c => new { c.Info.Name, c.Info.CompanyCode })
                .FirstOrDefaultAsync(ct);

            return row is null
                ? Results.NotFound()
                : Results.Ok(new { value = id.ToString(), text = LookupLabels.Client(row.Name, row.CompanyCode) });
        });

        return app;
    }

    /// <summary>The label of a client row for picker echoes; empty when the client is unknown.</summary>
    public static async Task<string> ClientLabelAsync(IQuery<Client> clients, Guid clientId, CancellationToken ct)
    {
        var row = await clients.All()
            .Where(c => c.Id == clientId)
            .Select(c => new { c.Info.Name, c.Info.CompanyCode })
            .FirstOrDefaultAsync(ct);

        return row is null ? string.Empty : LookupLabels.Client(row.Name, row.CompanyCode);
    }

    private static (IReadOnlyList<LookupItem> Items, bool HasMore) Page<T>(
        List<T> rows,
        int pageSize,
        Func<T, LookupItem> map)
    {
        var hasMore = rows.Count > pageSize;
        return (rows.Take(pageSize).Select(map).ToArray(), hasMore);
    }

    private static IResult Fragment(LookupRequest request, (IReadOnlyList<LookupItem> Items, bool HasMore) page) =>
        HtmxFragmentResult.Create(_LookupItems.Create(request.ToResponse(page.Items, page.HasMore))).Build();

    /// <summary>Display labels for lookup rows — the single source for search results and echoes.</summary>
    public static class LookupLabels
    {
        public static string Project(string? code, string name) =>
            string.IsNullOrWhiteSpace(code) ? name : $"{name} ({code})";

        public static string Client(string name, string? companyCode) =>
            string.IsNullOrWhiteSpace(companyCode) ? name : $"{name} ({companyCode})";
    }
}
