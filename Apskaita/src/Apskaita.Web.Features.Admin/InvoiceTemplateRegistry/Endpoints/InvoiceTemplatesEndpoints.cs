using Apskaita.Web.Features.Admin.InvoiceTemplateRegistry.Models;
using Apskaita.Web.Features.Admin.InvoiceTemplateRegistry.Services;
using Apskaita.Web.Features.Admin.InvoiceTemplateRegistry.Slices;
using Apskaita.Web.Features.Admin.Common.Models;
using Apskaita.Web.Features.Admin.Common.Slices;
using Apskaita.Web.Shared;
using FluentValidation;


namespace Apskaita.Web.Features.Admin.InvoiceTemplateRegistry.Endpoints;

public sealed class InvoiceTemplatesEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModuleWithWorkspace<InvoiceTemplateRegistryWorkspaceFactory>(resultBuilder)
{
    private readonly InvoiceTemplatesContentService _content = new();
    public override string FeatureDivId => "feature-details";

    public override IReadOnlyList<AdminNavItem> NavigationItems =>
    [
        new("ADMIN", "invoice-templates", "Invoice Templates", "list", Order: 50, AuthorizationPolicy: AuthPolicies.Admin)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.RequireAuthorization(AuthPolicies.Admin);

        group.MapGet("list", async (
            HttpContext ctx,
            [AsParameters] InvoiceTemplateSearchModel search,
            InvoiceTemplateListQuery query,
            ApskaitaDbContext db,
            CancellationToken ct) =>
        {
            var model = await _content.CreateListModelAsync(db, search, query, ct);
            if (ctx.IsHtmxTarget("invoice-templates-list"))
            {
                return Result.Fragment(_InvoiceTemplatesList.Create(model)).WithState(search).WithState(query);
            }

            return Result.Page(_ListPage.Create(model))
                .WithState(search)
                .WithState(query);
        });

        group.MapGet("create", () =>
            Result.Page(_CreatePage.Create(_content.CreateCreateFormModel())));

        group.MapPost("create", async (
            InvoiceTemplateUpsertRequest request,
            IValidator<InvoiceTemplateUpsertRequest> validator,
            ApskaitaDbContext db,
            CancellationToken ct) =>
        {
            _content.TrimRequest(request);
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                request.Errors = validationResult.ToErrorDictionary();
                return Result.Page(_CreatePage.Create(request));
            }

            await _content.CreateAsync(db, request, ct);
            return Result.Page(_CreatePage.Create(_content.CreateCreateFormModel())).WithTrigger("InvoiceTemplates.ListRefresh").WithToast($"Invoice template '{request.Name}' was created.");
        });

        group.MapGet("edit/{id:guid}", async (Guid id, ApskaitaDbContext db, CancellationToken ct) =>
        {
            var row = await _content.FindByIdAsync(db, id, ct);
            if (row is null) return Results.NotFound();
            return Result.Page(_EditPage.Create(_content.CreateEditFormModel(row)));
        });

        group.MapPost("edit/{id:guid}", async (
            Guid id,
            InvoiceTemplateUpsertRequest request,
            IValidator<InvoiceTemplateUpsertRequest> validator,
            ApskaitaDbContext db,
            CancellationToken ct) =>
        {
            var row = await _content.FindByIdAsync(db, id, ct);
            if (row is null) return Results.NotFound();

            _content.TrimRequest(request);
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                request.Errors = validationResult.ToErrorDictionary();
                request.IsActive = row.IsActive;
                return Result.Page(_EditPage.Create(request));
            }

            await _content.ApplyAsync(db, row, request, ct);
            return Result.Page(_EditPage.Create(_content.CreateEditFormModel(row))).WithTrigger("InvoiceTemplates.ListRefresh").WithToast($"Invoice template '{request.Name}' was updated.");
        });

        group.MapPost("toggle/{id:guid}", async (Guid id, HttpContext ctx, ApskaitaDbContext db, CancellationToken ct) =>
        {
            var row = await _content.FindByIdAsync(db, id, ct);
            if (row is null) return Results.NotFound();

            row.IsActive = !row.IsActive;
            row.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            var model = new StatusToggleModel(id, row.IsActive, ctx.Request.Path.Value ?? string.Empty);
            return Result.Fragment(_StatusAction.Create(model))
                .WithOob("#invoice-template-status-badge", _StatusBadge.Create(model))
                .WithToast($"Invoice template '{row.Name}' was {(row.IsActive ? "activated" : "deactivated")}.",
                    title: "Status", tone: row.IsActive ? ToastTone.Success : ToastTone.Warning);
        });

        group.MapGet("delete/{id:guid}", async (Guid id, ApskaitaDbContext db, CancellationToken ct) =>
        {
            var row = await _content.FindByIdAsync(db, id, ct);
            if (row is null) return Results.NotFound();
            return Result.ShowDialog(_InvoiceTemplateDelete.Create(_content.CreateDeleteDialogModel(row)));
        });

        group.MapPost("delete/{id:guid}", async (Guid id, ApskaitaDbContext db, CancellationToken ct) =>
        {
            var row = await _content.FindByIdAsync(db, id, ct);
            if (row is null) return Results.NotFound();

            await _content.DeleteAsync(db, row, ct);
            return Result.Page(_Empty.Create())
                .WithTrigger("InvoiceTemplates.ListRefresh")
                .WithToast($"Invoice template '{row.Name}' was deleted.", title: "Deleted", tone: ToastTone.Warning)
                .ClearDialog();
        });
    }
}

