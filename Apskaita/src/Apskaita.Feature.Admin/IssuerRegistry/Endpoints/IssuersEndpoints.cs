using Apskaita.Entities.Contracts;
using Apskaita.Feature.Admin.IssuerRegistry.Models;
using Apskaita.Feature.Admin.IssuerRegistry.Slices;
using Apskaita.Feature.Admin.Common.Models;
using Apskaita.Feature.Admin.Common.Slices;
using Apskaita.Feature.Admin.Persistence;
using Apskaita.Web.Shared;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Feature.Admin.IssuerRegistry.Endpoints;

public sealed class IssuersEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModuleWithWorkspace<IssuerRegistryWorkspaceFactory>(resultBuilder)
{
    public override string FeatureDivId => "feature-details";

    public override IReadOnlyList<AdminNavItem> NavigationItems =>
    [
        new("ADMIN", "issuers", "Issuers", "list", Order: 20, AuthorizationPolicy: AuthPolicies.Admin)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.RequireAuthorization(AuthPolicies.Admin);

        // GET list — read seam (IQuery<Issuer>).
        group.MapGet("list", async (
            HttpContext ctx,
            [AsParameters] IssuerSearchModel search,
            IssuerListQuery query,
            IQuery<Issuer> issuers,
            CancellationToken ct) =>
        {
            var items = issuers.All().Select(c => new IssuerRowModel
            {
                Id = c.Id,
                Name = c.Info.Name,
                CompanyCode = c.Info.CompanyCode,
                VatCode = c.Info.VatCode,
                ContactEmail = c.Info.ContactEmail,
                IsActive = c.IsActive
            });

            if (!string.IsNullOrWhiteSpace(search.Search))
            {
                var term = search.Search.Trim();
                items = items.Where(c =>
                    c.Name.Contains(term) ||
                    (c.CompanyCode != null && c.CompanyCode.Contains(term)) ||
                    (c.VatCode != null && c.VatCode.Contains(term)) ||
                    (c.ContactEmail != null && c.ContactEmail.Contains(term)));
            }

            var paged = await items.ToPagedListAsync(query, ct);
            var model = new IssuerListModel(paged.Total, search, query, paged.Items);

            if (ctx.IsHtmxTarget("issuers-list"))
            {
                return Result.Fragment(_IssuersList.Create(model)).WithState(search).WithState(query);
            }

            return Result.Page(_ListPage.Create(model)).WithState(search).WithState(query);
        });

        group.MapGet("create", () =>
            Result.Page(_CreatePage.Create(IssuerUpsertRequest.CreateDefault())));

        // POST create — write seam (IAdminStore).
        group.MapPost("create", async (
            IssuerUpsertRequest request,
            IValidator<IssuerUpsertRequest> validator,
            IAdminStore store,
            CancellationToken ct) =>
        {
            request.Normalize();
            var validationResult = await validator.ValidateAsync(request, ct);

            if (!validationResult.IsValid)
            {
                request.Errors = validationResult.ToErrorDictionary();
                return Result.Page(_CreatePage.Create(request));
            }

            var now = DateTime.UtcNow;
            store.Issuers.Add(new Issuer
            {
                Id = Guid.NewGuid(),
                Info = request.ToCompanyInfo(),
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                CreatedByUserId = Guid.Empty,
                UpdatedByUserId = Guid.Empty
            });
            await store.SaveChangesAsync(ct);

            return Result.Page(_CreatePage.Create(IssuerUpsertRequest.CreateDefault()))
                .WithTrigger("Issuers.ListRefresh")
                .WithToast($"Issuer '{request.Name}' was created.");
        });

        // GET edit — read seam (no-tracking find for display).
        group.MapGet("edit/{id:guid}", async (Guid id, IQuery<Issuer> issuers, CancellationToken ct) =>
        {
            var row = await issuers.All().FirstOrDefaultAsync(c => c.Id == id, ct);
            if (row is null) return Results.NotFound();
            return Result.Page(_EditPage.Create(IssuerUpsertRequest.FromEntity(row)));
        });

        // POST edit — write seam (tracked load for mutation).
        group.MapPost("edit/{id:guid}", async (
            Guid id,
            IssuerUpsertRequest request,
            IValidator<IssuerUpsertRequest> validator,
            IAdminStore store,
            CancellationToken ct) =>
        {
            var row = await store.Issuers.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (row is null) return Results.NotFound();

            request.Normalize();
            var validationResult = await validator.ValidateAsync(request, ct);

            if (!validationResult.IsValid)
            {
                request.Errors = validationResult.ToErrorDictionary();
                request.IsActive = row.IsActive;
                return Result.Page(_EditPage.Create(request));
            }

            row.Info = request.ToCompanyInfo();
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.UpdatedByUserId = Guid.Empty;
            await store.SaveChangesAsync(ct);

            return Result.Page(_EditPage.Create(IssuerUpsertRequest.FromEntity(row)))
                .WithTrigger("Issuers.ListRefresh")
                .WithToast($"Issuer '{request.Name}' was updated.");
        });

        // POST toggle — write seam (tracked load + mutate).
        group.MapPost("toggle/{id:guid}", async (Guid id, HttpContext ctx, IAdminStore store, CancellationToken ct) =>
        {
            var row = await store.Issuers.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (row is null) return Results.NotFound();

            row.IsActive = !row.IsActive;
            row.UpdatedAtUtc = DateTime.UtcNow;
            await store.SaveChangesAsync(ct);

            var model = new StatusToggleModel(id, row.IsActive, ctx.Request.Path.Value ?? string.Empty);
            return Result.Fragment(_StatusAction.Create(model))
                .WithOob("#issuer-status-badge", _StatusBadge.Create(model))
                .WithToast($"Issuer '{row.Info.Name}' was {(row.IsActive ? "activated" : "deactivated")}.",
                    title: "Status", tone: row.IsActive ? ToastTone.Success : ToastTone.Warning);
        });

        // GET delete — read seam (no-tracking find for the dialog).
        group.MapGet("delete/{id:guid}", async (Guid id, IQuery<Issuer> issuers, CancellationToken ct) =>
        {
            var row = await issuers.All().FirstOrDefaultAsync(c => c.Id == id, ct);
            if (row is null) return Results.NotFound();
            return Result.ShowDialog(_IssuerDelete.Create(new IssuerDeleteDialogModel(row.Id, row.Info.Name)));
        });

        // POST delete — write seam (tracked load + remove).
        group.MapPost("delete/{id:guid}", async (Guid id, IAdminStore store, CancellationToken ct) =>
        {
            var row = await store.Issuers.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (row is null) return Results.NotFound();

            store.Issuers.Remove(row);
            await store.SaveChangesAsync(ct);

            return Result.Page(_Empty.Create())
                .WithTrigger("Issuers.ListRefresh")
                .WithToast($"Issuer '{row.Info.Name}' was deleted.", title: "Deleted", tone: ToastTone.Warning)
                .ClearDialog();
        });
    }
}
