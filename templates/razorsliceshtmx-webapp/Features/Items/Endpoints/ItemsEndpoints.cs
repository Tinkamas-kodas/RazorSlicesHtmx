using Microsoft.AspNetCore.Mvc;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Results;
using RshtmxApp.Features.Items;
using RshtmxApp.Features.Items.Models;
using RshtmxApp.Features.Items.Services;
using RshtmxApp.Features.Items.Slices;
//#if (fluentValidation)
using FluentValidation;
using RazorSlicesHtmx.FluentValidation.Extensions;
//#endif

namespace RshtmxApp.Features.Items.Endpoints;

public sealed class ItemsEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    private readonly ItemsContentService _content = new();

    public override IReadOnlyList<NavigationItem> NavigationItems =>
    [
        new("items", "Items", "/items/list", Order: 10)
    ];

    public override void MapEndpoints(WebApplication app)
    {
        app.MapGet("/items/list", (HttpContext ctx, [AsParameters] ItemSearchModel search, ItemListQuery query) =>
        {
            var model = _content.CreateListModel(search, query);
            if (ctx.IsHtmxTarget("items-list"))
            {
                return Result.Page(_ItemsList.Create(model))
                    .WithState(search)
                    .WithState(query)
                    ;
            }
            return Result.Page(_ListPage.Create(model))
                .WithWorkspace<ItemsWorkspaceFactory>()
                .WithState(search)
                .WithState(query)
                ;
        });

        app.MapGet("/items/create", () =>
            Result.Page(_CreatePage.Create(_content.CreateCreateFormModel()))
                .WithWorkspace<ItemsWorkspaceFactory>()
                );

//#if (fluentValidation)
        app.MapPost("/items/create", async (ItemUpsertRequest request, IValidator<ItemUpsertRequest> validator, CancellationToken ct) =>
            {
                _content.TrimRequest(request);
                var validationResult = await validator.ValidateAsync(request, ct);

                if (!validationResult.IsValid)
                {
                    request.Errors = validationResult.ToErrorDictionary();
                    return Result.Page(_CreatePage.Create(request))
                        ;
                }

                _content.Create(request);

                return Result.Page(_Empty.Create())
                    .WithTrigger("Items.ListRefresh")
                    .WithToast($"Item '{request.Name}' was created.")
                    ;
            });
//#endif
//#if (!fluentValidation)
        app.MapPost("/items/create", async (HttpContext http, CancellationToken ct) =>
            {
                var request = await _content.ReadUpsertRequestAsync(http.Request);
                _content.TrimRequest(request);
                _content.Create(request);

                return Result.Page(_Empty.Create())
                    .WithTrigger("Items.ListRefresh")
                    .WithToast($"Item '{request.Name}' was created.")
                    ;
            });
//#endif

        app.MapGet("/items/edit/{id:int}", (int id) =>
        {
            var row = _content.FindById(id);
            if (row is null)
                return Results.NotFound();

            return Result.Page(_EditPage.Create(_content.CreateEditFormModel(row)))
                .WithWorkspace<ItemsWorkspaceFactory>()
                ;
        });

//#if (fluentValidation)
        app.MapPost("/items/edit/{id:int}", async (int id, ItemUpsertRequest request, IValidator<ItemUpsertRequest> validator, CancellationToken ct) =>
            {
                var row = _content.FindById(id);
                if (row is null)
                    return Results.NotFound();

                _content.TrimRequest(request);
                var validationResult = await validator.ValidateAsync(request, ct);

                if (!validationResult.IsValid)
                {
                    request.Errors = validationResult.ToErrorDictionary();
                    return Result.Page(_EditPage.Create(request))
                        ;
                }

                _content.Apply(id, request);

                return Result.Page(_Empty.Create())
                    .WithTrigger("Items.ListRefresh")
                    .WithToast($"Item '{request.Name}' was updated.")
                    ;
            });
//#endif
//#if (!fluentValidation)
        app.MapPost("/items/edit/{id:int}", async (int id, ItemUpsertRequest request, CancellationToken ct) =>
            {
                var row = _content.FindById(id);
                if (row is null)
                    return Results.NotFound();

                _content.TrimRequest(request);
                _content.Apply(id, request);

                return Result.Page(_Empty.Create())
                    .WithTrigger("Items.ListRefresh")
                    .WithToast($"Item '{request.Name}' was updated.")
                    ;
            });
//#endif

        app.MapGet("/items/delete/{id:int}", (int id) =>
        {
            var row = _content.FindById(id);
            if (row is null)
                return Results.NotFound();

            return Result.ShowDialog(_ItemDelete.Create(_content.CreateDeleteDialogModel(row)));
        });

        app.MapPost("/items/delete/{id:int}", async (int id, CancellationToken ct) =>
            {
                var row = _content.FindById(id);
                if (row is null)
                    return Results.NotFound();

                _content.Delete(id);

                return Result.Page(_Empty.Create())
                    .WithTrigger("Items.ListRefresh")
                    .WithToast($"Item '{row.Name}' was deleted.", title: "Deleted", tone: ToastTone.Warning)
                    .ClearDialog()
                    ;
            });
    }
}
