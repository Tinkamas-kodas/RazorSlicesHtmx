using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using razr_slices_htmx2.Data;
using razr_slices_htmx2.Features.Items.Models;
using razr_slices_htmx2.Features.Items.Services;
using razr_slices_htmx2.Features.Items.Slices;
using razr_slices_htmx2.Shared.Models;

namespace razr_slices_htmx2.Features.Items.Endpoints;

public sealed class ItemsEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    private readonly ItemsContentService _content = new();

    public override PageDefinition Page => _content.CreatePageDefinition();

    public override void MapEndpoints(WebApplication app)
    {
        app.MapGet("/items/list", (AppDbContext db) =>
        {
            var model = _content.CreateListModel(db, _content.CreateQuery(Result.Request.Query));
            return Result.Create(_ItemsPage.Create(model))
                .AsFragment(_ItemsWorkspace.Create(model))
                .Build();
        });

        app.MapGet("/items/create", () =>
            Result.Create(_ItemFormPage.Create(_content.CreateCreateFormModel(_content.CreateQuery(Result.Request.Query))))
                .Build());

        app.MapPost("/items/create", ([FromForm] ItemUpsertRequest request, AppDbContext db, IValidator<ItemUpsertRequest> validator) =>
            {
                _content.TrimRequest(request);
                var validationResult = validator.Validate(request);

                if (!validationResult.IsValid)
                {
                    return Result.Create(_ItemFormPage.Create(_content.CreateInvalidFormModel(request, validationResult, false)))
                        .Build();
                }

                var entity = _content.CreateEntity(request);
                db.Items.Add(entity);
                db.SaveChanges();

                var returnUrl = _content.CreateQuery(request).ToRoute("/items");

                return Result.Create(_Empty.Create())
                    .WithToast($"Item '{entity.Code}' was created.")
                    .WithLocation(returnUrl)
                    .Build();
            })
            .DisableAntiforgery();

        app.MapGet("/items/edit/{id:int}", (int id, AppDbContext db) =>
        {
            var entity = db.Items.Find(id);
            if (entity is null)
            {
                return Results.NotFound();
            }

            return Result.Create(_ItemFormPage.Create(_content.CreateEditFormModel(entity, _content.CreateQuery(Result.Request.Query))))
                .Build();
        });

        app.MapPost("/items/edit/{id:int}", (int id, [FromForm] ItemUpsertRequest request, AppDbContext db, IValidator<ItemUpsertRequest> validator) =>
            {
                var entity = db.Items.Find(id);
                if (entity is null)
                {
                    return Results.NotFound();
                }

                request.Id = id;
                _content.TrimRequest(request);
                var validationResult = validator.Validate(request);

                if (!validationResult.IsValid)
                {
                    return Result.Create(_ItemFormPage.Create(_content.CreateInvalidFormModel(request, validationResult, true)))
                        .Build();
                }

                _content.Apply(entity, request);
                db.SaveChanges();

                var returnUrl = _content.CreateQuery(request).ToRoute("/items");

                return Result.Create(_Empty.Create())
                    .WithToast($"Item '{entity.Code}' was updated.")
                    .WithLocation(returnUrl)
                    .Build();
            })
            .DisableAntiforgery();

        app.MapGet("/items/delete/{id:int}", (int id, AppDbContext db) =>
        {
            var entity = db.Items.Find(id);
            if (entity is null)
            {
                return Results.NotFound();
            }

            return Result.Dialog(_DeleteItemDialogContent.Create(_content.CreateDeleteDialogModel(entity, _content.CreateQuery(Result.Request.Query))));
        });

        app.MapPost("/items/delete/{id:int}", (int id, [FromForm] ItemDeleteRequest request, AppDbContext db) =>
            {
                var entity = db.Items.Find(id);
                if (entity is null)
                {
                    return Results.NotFound();
                }

                db.Items.Remove(entity);
                db.SaveChanges();

                return Result.Create(_Empty.Create())
                    .WithTrigger("Items.ListRefresh")
                    .WithToast($"Item '{entity.Code}' was deleted.", title: "Deleted", tone: "dark")
                    .ClearDialog()
                    .Build();
            })
            .DisableAntiforgery();
    }
}