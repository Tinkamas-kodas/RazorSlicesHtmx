using Apskaita.Entities.Contracts;
using Apskaita.Feature.Admin.InvoiceTemplateRegistry.Models;
using Apskaita.Feature.Admin.InvoiceTemplateRegistry.Slices;
using Apskaita.Shared.Ui.Common.Models;
using Apskaita.Shared.Ui.Common.Slices;
using Apskaita.Feature.Admin.Persistence;
using Apskaita.Shared;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Feature.Admin.InvoiceTemplateRegistry.Endpoints;

public sealed class InvoiceTemplatesEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModuleWithWorkspace<InvoiceTemplateRegistryWorkspaceFactory>(resultBuilder)
{
    public override string FeatureDivId => "feature-details";

    public override IReadOnlyList<AdminNavItem> NavigationItems =>
    [
        new("ADMIN", "invoice-templates", "Invoice Templates", "list", Order: 50, AuthorizationPolicy: AuthPolicies.Admin)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.RequireAuthorization(AuthPolicies.Admin);

        // GET list — read seam (IQuery<InvoiceTemplate>).
        group.MapGet("list", async (
            HttpContext ctx,
            [AsParameters] InvoiceTemplateSearchModel search,
            InvoiceTemplateListQuery query,
            IQuery<InvoiceTemplate> templates,
            CancellationToken ct) =>
        {
            var items = templates.All().Select(x => new InvoiceTemplateRowModel
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                LanguageCode = x.LanguageCode,
                IsDefault = x.IsDefault,
                IsActive = x.IsActive
            });

            if (!string.IsNullOrWhiteSpace(search.Search))
            {
                var term = search.Search.Trim();
                items = items.Where(x => x.Name.Contains(term) || x.Code.Contains(term) || x.LanguageCode.Contains(term));
            }

            var paged = await items.ToPagedListAsync(query, ct);
            var model = new InvoiceTemplateListModel(paged.Total, search, query, paged.Items);

            if (ctx.IsHtmxTarget("invoice-templates-list"))
            {
                return Result.Fragment(_InvoiceTemplatesList.Create(model)).WithState(search).WithState(query);
            }

            return Result.Page(_ListPage.Create(model)).WithState(search).WithState(query);
        });

        group.MapGet("create", () =>
            Result.Page(_CreatePage.Create(InvoiceTemplateUpsertRequest.CreateDefault())));

        // POST create — write seam (IAdminStore).
        group.MapPost("create", async (
            InvoiceTemplateUpsertRequest request,
            IValidator<InvoiceTemplateUpsertRequest> validator,
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

            if (request.IsDefault)
            {
                await ClearDefaultAsync(store, ct);
            }

            var now = DateTime.UtcNow;
            store.InvoiceTemplates.Add(new InvoiceTemplate
            {
                Id = Guid.NewGuid(),
                Code = request.Code ?? string.Empty,
                Name = request.Name ?? string.Empty,
                LanguageCode = request.LanguageCode ?? "LTU",
                Content = request.Content ?? string.Empty,
                IsDefault = request.IsDefault,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                CreatedByUserId = Guid.Empty,
                UpdatedByUserId = Guid.Empty
            });
            await store.SaveChangesAsync(ct);

            return Result.Page(_CreatePage.Create(InvoiceTemplateUpsertRequest.CreateDefault()))
                .WithTrigger("InvoiceTemplates.ListRefresh")
                .WithToast($"Invoice template '{request.Name}' was created.");
        });

        // GET edit — read seam (no-tracking find for display).
        group.MapGet("edit/{id:guid}", async (Guid id, IQuery<InvoiceTemplate> templates, CancellationToken ct) =>
        {
            var row = await templates.All().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (row is null) return Results.NotFound();
            return Result.Page(_EditPage.Create(InvoiceTemplateUpsertRequest.FromEntity(row)));
        });

        // POST edit — write seam (tracked load for mutation).
        group.MapPost("edit/{id:guid}", async (
            Guid id,
            InvoiceTemplateUpsertRequest request,
            IValidator<InvoiceTemplateUpsertRequest> validator,
            IAdminStore store,
            CancellationToken ct) =>
        {
            var row = await store.InvoiceTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (row is null) return Results.NotFound();

            request.Normalize();
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                request.Errors = validationResult.ToErrorDictionary();
                request.IsActive = row.IsActive;
                return Result.Page(_EditPage.Create(request));
            }

            row.Code = request.Code ?? string.Empty;
            row.Name = request.Name ?? string.Empty;
            row.LanguageCode = request.LanguageCode ?? "LTU";
            row.Content = request.Content ?? string.Empty;
            row.IsDefault = request.IsDefault;
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.UpdatedByUserId = Guid.Empty;

            if (row.IsDefault)
            {
                await ClearDefaultAsync(store, ct, row.Id);
            }

            await store.SaveChangesAsync(ct);

            return Result.Page(_EditPage.Create(InvoiceTemplateUpsertRequest.FromEntity(row)))
                .WithTrigger("InvoiceTemplates.ListRefresh")
                .WithToast($"Invoice template '{request.Name}' was updated.");
        });

        // POST toggle — write seam (tracked load + mutate).
        group.MapPost("toggle/{id:guid}", async (Guid id, HttpContext ctx, IAdminStore store, CancellationToken ct) =>
        {
            var row = await store.InvoiceTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (row is null) return Results.NotFound();

            row.IsActive = !row.IsActive;
            row.UpdatedAtUtc = DateTime.UtcNow;
            await store.SaveChangesAsync(ct);

            var model = new StatusToggleModel(id, row.IsActive, ctx.Request.Path.Value ?? string.Empty);
            return Result.Fragment(_StatusAction.Create(model))
                .WithOob("#invoice-template-status-badge", _StatusBadge.Create(model))
                .WithToast($"Invoice template '{row.Name}' was {(row.IsActive ? "activated" : "deactivated")}.",
                    title: "Status", tone: row.IsActive ? ToastTone.Success : ToastTone.Warning);
        });

        // GET delete — read seam (no-tracking find for the dialog).
        group.MapGet("delete/{id:guid}", async (Guid id, IQuery<InvoiceTemplate> templates, CancellationToken ct) =>
        {
            var row = await templates.All().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (row is null) return Results.NotFound();
            return Result.ShowDialog(_InvoiceTemplateDelete.Create(new InvoiceTemplateDeleteDialogModel(row.Id, row.Name)));
        });

        // POST delete — write seam (tracked load + remove).
        group.MapPost("delete/{id:guid}", async (Guid id, IAdminStore store, CancellationToken ct) =>
        {
            var row = await store.InvoiceTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (row is null) return Results.NotFound();

            store.InvoiceTemplates.Remove(row);
            await store.SaveChangesAsync(ct);

            return Result.Page(_Empty.Create())
                .WithTrigger("InvoiceTemplates.ListRefresh")
                .WithToast($"Invoice template '{row.Name}' was deleted.", title: "Deleted", tone: ToastTone.Warning)
                .ClearDialog();
        });
    }

    private static async Task ClearDefaultAsync(IAdminStore store, CancellationToken ct, Guid? exceptId = null)
    {
        var defaults = store.InvoiceTemplates.Where(x => x.IsDefault && (!exceptId.HasValue || x.Id != exceptId.Value));
        await defaults.ForEachAsync(x => x.IsDefault = false, ct);
    }
}
