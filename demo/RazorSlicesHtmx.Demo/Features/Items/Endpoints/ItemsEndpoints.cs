using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using RazorSlicesHtmx.Demo.Data;
using RazorSlicesHtmx.Demo.Features.Items.Models;
using RazorSlicesHtmx.Demo.Features.Items.Services;
using RazorSlicesHtmx.Demo.Features.Items.Slices;

namespace RazorSlicesHtmx.Demo.Features.Items.Endpoints;

public sealed class ItemsEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    private readonly ItemsContentService _content = new();

    public override PageDefinition Page => _content.CreatePageDefinition();

    public override void MapEndpoints(WebApplication app)
    {
        app.MapGet("/items/list", (AppDbContext db, [AsParameters] ItemSearchModel search, ItemListQuery query) =>
        {
            var model = _content.CreateListModel(db, search, query);
            return Result.For(_FeaturePage.Create(model))
                .AsFragment(_ListPage.Create(model))
                .WithState(search)
                .WithState(query)
                .Build();
        });

        app.MapGet("/items/create", () =>
            Result.For(_CreatePage.Create(_content.CreateCreateFormModel()))
                .Build());

        app.MapPost("/items/create", ([FromForm] ItemUpsertRequest request, AppDbContext db, IValidator<ItemUpsertRequest> validator) =>
            {
                _content.TrimRequest(request);
                var validationResult = validator.Validate(request);

                if (!validationResult.IsValid)
                {
                    request.Errors = validationResult.ToErrorDictionary();
                    return Result.For(_CreatePage.Create(request))
                        .Build();
                }

                var entity = _content.CreateEntity(request);
                db.Items.Add(entity);
                db.SaveChanges();

                return Result.For(_Empty.Create())
                    .WithTrigger("Items.ListRefresh")
                    .WithToast($"Item '{entity.Code}' was created.")
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

            return Result.For(_EditPage.Create(_content.CreateEditFormModel(entity)))
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
                    request.Errors = validationResult.ToErrorDictionary();
                    return Result.For(_EditPage.Create(request))
                        .Build();
                }

                _content.Apply(entity, request);
                db.SaveChanges();

                return Result.For(_Empty.Create())
                    .WithTrigger("Items.ListRefresh")
                    .WithToast($"Item '{entity.Code}' was updated.")
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

            return Result.Dialog(_ItemDelete.Create(_content.CreateDeleteDialogModel(entity)));
        });

        app.MapPost("/items/delete/{id:int}", (int id, AppDbContext db) =>
            {
                var entity = db.Items.Find(id);
                if (entity is null)
                {
                    return Results.NotFound();
                }

                db.Items.Remove(entity);
                db.SaveChanges();

                return Result.For(_Empty.Create())
                    .WithTrigger("Items.ListRefresh")
                    .WithToast($"Item '{entity.Code}' was deleted.", title: "Deleted", tone: "dark")
                    .ClearDialog()
                    .Build();
            })
            .DisableAntiforgery();
    }
}