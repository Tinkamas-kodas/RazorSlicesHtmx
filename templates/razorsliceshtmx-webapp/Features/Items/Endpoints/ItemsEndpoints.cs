using Microsoft.AspNetCore.Mvc;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Results;
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

    public override IReadOnlyList<NavigationItem> NavigationItems => [_content.CreateNavigationItem()];

    public override void MapEndpoints(WebApplication app)
    {
        app.MapGet("/items/list", ([AsParameters] ItemSearchModel search, ItemListQuery query) =>
        {
            var model = _content.CreateListModel(search, query);
            return Result.For(_FeaturePage.Create(model))
                .AsFragment(_ListPage.Create(model))
                .WithState(search)
                .WithState(query)
                .BuildAsync();
        });

        app.MapGet("/items/create", () =>
            Result.For(_CreatePage.Create(_content.CreateCreateFormModel()))
                .BuildAsync());

//#if (fluentValidation)
        app.MapPost("/items/create", async (ItemUpsertRequest request, IValidator<ItemUpsertRequest> validator, CancellationToken ct) =>
            {
                _content.TrimRequest(request);
                var validationResult = await validator.ValidateAsync(request, ct);

                if (!validationResult.IsValid)
                {
                    request.Errors = validationResult.ToErrorDictionary();
                    return await Result.For(_CreatePage.Create(request))
                        .BuildAsync(ct);
                }

                _content.Create(request);

                return await Result.For(_Empty.Create())
                    .WithTrigger("Items.ListRefresh")
                    .WithToast($"Item '{request.Name}' was created.")
                    .BuildAsync(ct);
            });
//#endif
//#if (!fluentValidation)
        app.MapPost("/items/create", async (HttpContext http, CancellationToken ct) =>
            {
                var request = await _content.ReadUpsertRequestAsync(http.Request);
                _content.TrimRequest(request);
                _content.Create(request);

                return await Result.For(_Empty.Create())
                    .WithTrigger("Items.ListRefresh")
                    .WithToast($"Item '{request.Name}' was created.")
                    .BuildAsync(ct);
            });
//#endif

        app.MapGet("/items/edit/{id:int}", (int id) =>
        {
            var row = _content.FindById(id);
            if (row is null)
                return Task.FromResult(Results.NotFound());

            return Result.For(_EditPage.Create(_content.CreateEditFormModel(row)))
                .BuildAsync();
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
                    return await Result.For(_EditPage.Create(request))
                        .BuildAsync(ct);
                }

                _content.Apply(id, request);

                return await Result.For(_Empty.Create())
                    .WithTrigger("Items.ListRefresh")
                    .WithToast($"Item '{request.Name}' was updated.")
                    .BuildAsync(ct);
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

                return await Result.For(_Empty.Create())
                    .WithTrigger("Items.ListRefresh")
                    .WithToast($"Item '{request.Name}' was updated.")
                    .BuildAsync(ct);
            });
//#endif

        app.MapGet("/items/delete/{id:int}", (int id) =>
        {
            var row = _content.FindById(id);
            if (row is null)
                return Results.NotFound();

            return Result.Dialog(_ItemDelete.Create(_content.CreateDeleteDialogModel(row)));
        });

        app.MapPost("/items/delete/{id:int}", async (int id, CancellationToken ct) =>
            {
                var row = _content.FindById(id);
                if (row is null)
                    return Results.NotFound();

                _content.Delete(id);

                return await Result.For(_Empty.Create())
                    .WithTrigger("Items.ListRefresh")
                    .WithToast($"Item '{row.Name}' was deleted.", title: "Deleted", tone: ToastTone.Warning)
                    .ClearDialog()
                    .BuildAsync(ct);
            });
    }
}
