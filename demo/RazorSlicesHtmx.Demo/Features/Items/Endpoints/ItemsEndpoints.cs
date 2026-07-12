using FluentValidation;
using RazorSlicesHtmx.Demo.Data;
using RazorSlicesHtmx.Demo.Features.Items.Models;
using RazorSlicesHtmx.Demo.Features.Items.Services;
using RazorSlicesHtmx.Demo.Features.Items.Slices;

namespace RazorSlicesHtmx.Demo.Features.Items.Endpoints;

public static class Consts
{
    public const string RefreshListTrigger = "Items.ListRefresh";
}
public sealed class ItemsEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    private readonly ItemsContentService _content = new();
    public override IReadOnlyList<NavigationItem> NavigationItems =>
    [
        new("items", "Items", "/items/list", Order: 200)
    ];

    public override void MapEndpoints(WebApplication app)
    {
        app.MapGet("/items/list", (HttpContext ctx, AppDbContext db, [AsParameters] ItemSearchModel search, ItemListQuery query) =>
        {
            var model = _content.CreateListModel(db, search, query);
            if (ctx.IsHtmxTarget("items-list"))
            {
                //refresh only list without search bar
                return Result.For(_ItemsList.Create(model))
                    .WithState(search)
                    .WithState(query)
                    .BuildAsync();
            }
            
            return Result.For(ListPage.Create(model))
                .WithWorkspace<ItemsWorkspaceFactory>()
                .WithState(search)
                .WithState(query)
                .BuildAsync();
        });

        app.MapGet("/items/create", () => Result
            .For(CreatePage.Create(_content.CreateCreateFormModel()))
            .WithWorkspace<ItemsWorkspaceFactory>()
            .BuildAsync());

        app.MapPost("/items/create", async (ItemUpsertRequest request, AppDbContext db,
                IValidator<ItemUpsertRequest> validator, CancellationToken ct) =>
            {
                _content.TrimRequest(request);
                var validationResult = await validator.ValidateAsync(request, ct);

                if (!validationResult.IsValid)
                {
                    request.Errors = validationResult.ToErrorDictionary();
                    return await Result.For(CreatePage.Create(request))
                        .BuildAsync(ct);
                }

                var entity = _content.CreateEntity(request);
                db.Items.Add(entity);
                await db.SaveChangesAsync(ct);

                return await Result.Empty()
                    .WithTrigger(Consts.RefreshListTrigger)
                    .WithToast($"Item '{entity.Code}' was created.")
                    .BuildAsync(ct);
            })
            .DisableAntiforgery();

        app.MapGet("/items/edit/{id:int}", (int id, AppDbContext db) =>
        {
            var entity = db.Items.Find(id);
            if (entity is null)
            {
                return Task.FromResult(Results.NotFound());
            }

            return Result
                .For(EditPage.Create(_content.CreateEditFormModel(entity)))
                .WithWorkspace<ItemsWorkspaceFactory>()
                .BuildAsync();
        });

        app.MapPost("/items/edit/{id:int}", async (int id, ItemUpsertRequest request, AppDbContext db,
                IValidator<ItemUpsertRequest> validator, CancellationToken ct) =>
            {
                var entity = await db.Items.FindAsync([id], ct);
                if (entity is null)
                {
                    return Results.NotFound();
                }

                request.Id = id;
                _content.TrimRequest(request);
                var validationResult = await validator.ValidateAsync(request, ct);

                if (!validationResult.IsValid)
                {
                    request.Errors = validationResult.ToErrorDictionary();
                    return await Result.For(EditPage.Create(request))
                        .BuildAsync(ct);
                }

                _content.Apply(entity, request);
                await db.SaveChangesAsync(ct);

                return await Result.Empty()
                    .WithTrigger(Consts.RefreshListTrigger)
                    .WithToast($"Item '{entity.Code}' was updated.")
                    .BuildAsync(ct);
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

        app.MapPost("/items/delete/{id:int}", async (int id, ItemDeleteDialogModel request,IValidator<ItemDeleteDialogModel> validator, AppDbContext db, CancellationToken ct) =>
            {
                var validationResult = await validator.ValidateAsync(request, ct);
                Console.WriteLine(validationResult.IsValid);
                if (!validationResult.IsValid)
                {
                    request.Errors = validationResult.ToErrorDictionary();
                    return await Result.For(_ItemDelete.Create(request))
                        .BuildAsync(ct);
                }

                var entity = await db.Items.FindAsync([id], ct);
                if (entity is null)
                {
                    return Results.NotFound();
                }

                db.Items.Remove(entity);
                await db.SaveChangesAsync(ct);

                return await Result.For(_ItemDelete.Create(request))
                    .WithTrigger(Consts.RefreshListTrigger)
                    .WithToast($"Item '{entity.Code}' was deleted.", title: "Deleted", tone: ToastTone.Warning)
                    .ClearDialog()
                    .BuildAsync(ct);
            })
            .DisableAntiforgery();
    }
}