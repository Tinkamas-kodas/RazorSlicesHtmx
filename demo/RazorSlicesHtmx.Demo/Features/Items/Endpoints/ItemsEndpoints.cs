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
        new("items", "Items", "list", Order: 200)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("list", (HttpContext ctx, AppDbContext db, [AsParameters] ItemSearchModel search, ItemListQuery query) =>
        {
            var model = _content.CreateListModel(db, search, query);
            if (ctx.IsHtmxTarget("items-list"))
            {
                //refresh only list without search bar
                return Result.Page(_ItemsList.Create(model))
                    .WithState(search)
                    .WithState(query)
                    ;
            }
            
            return Result.Page(ListPage.Create(model))
                .WithWorkspace<ItemsWorkspaceFactory>()
                .WithState(search)
                .WithState(query)
                ;
        });

        group.MapGet("create", () => Result
            .Page(CreatePage.Create(_content.CreateCreateFormModel()))
            .WithWorkspace<ItemsWorkspaceFactory>()
            );

        group.MapPost("create", async (ItemUpsertRequest request, AppDbContext db,
                IValidator<ItemUpsertRequest> validator, CancellationToken ct) =>
            {
                _content.TrimRequest(request);
                var validationResult = await validator.ValidateAsync(request, ct);

                if (!validationResult.IsValid)
                {
                    request.Errors = validationResult.ToErrorDictionary();
                    return Result.Page(CreatePage.Create(request))
                        ;
                }

                var entity = _content.CreateEntity(request);
                db.Items.Add(entity);
                await db.SaveChangesAsync(ct);

                return Result.Empty()
                    .WithTrigger(Consts.RefreshListTrigger)
                    .WithToast($"Item '{entity.Code}' was created.")
                    ;
            })
            .DisableAntiforgery();

        group.MapGet("edit/{id:int}", (int id, AppDbContext db) =>
        {
            var entity = db.Items.Find(id);
            if (entity is null)
            {
                return Results.NotFound();
            }

            return Result
                .Page(EditPage.Create(_content.CreateEditFormModel(entity)))
                .WithWorkspace<ItemsWorkspaceFactory>()
                ;
        });

        group.MapPost("edit/{id:int}", async (int id, ItemUpsertRequest request, AppDbContext db,
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
                    return Result.Page(EditPage.Create(request))
                        ;
                }

                _content.Apply(entity, request);
                await db.SaveChangesAsync(ct);

                return Result.Empty()
                    .WithTrigger(Consts.RefreshListTrigger)
                    .WithToast($"Item '{entity.Code}' was updated.")
                    ;
            })
            .DisableAntiforgery();

        group.MapGet("delete/{id:int}", (int id, AppDbContext db) =>
        {
            var entity = db.Items.Find(id);
            if (entity is null)
            {
                return Results.NotFound();
            }

            return Result.ShowDialog(_ItemDelete.Create(_content.CreateDeleteDialogModel(entity)));
        });

        group.MapPost("delete/{id:int}", async (int id, ItemDeleteDialogModel request,IValidator<ItemDeleteDialogModel> validator, AppDbContext db, CancellationToken ct) =>
            {
                var validationResult = await validator.ValidateAsync(request, ct);
                Console.WriteLine(validationResult.IsValid);
                if (!validationResult.IsValid)
                {
                    request.Errors = validationResult.ToErrorDictionary();
                    return Result.Page(_ItemDelete.Create(request))
                        ;
                }

                var entity = await db.Items.FindAsync([id], ct);
                if (entity is null)
                {
                    return Results.NotFound();
                }

                db.Items.Remove(entity);
                await db.SaveChangesAsync(ct);

                return Result.Page(_ItemDelete.Create(request))
                    .WithTrigger(Consts.RefreshListTrigger)
                    .WithToast($"Item '{entity.Code}' was deleted.", title: "Deleted", tone: ToastTone.Warning)
                    .ClearDialog()
                    ;
            })
            .DisableAntiforgery();
    }
}