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
        // ─── LIST ───

        app.MapGet("/items/list", ([AsParameters] ItemSearch search, [AsParameters] ItemListQuery query) =>
        {
            var model = _content.CreateListModel(search, query);
            return Result.For(_ListPage.Create(model))
                .AsFragment(_ItemsTable.Create(model))
                .WithState(search)
                .WithState(query)
                .BuildAsync();
        });

        // ─── CREATE ───

        app.MapGet("/items/create", () =>
            Result.For(_CreatePage.Create(new ItemUpsertRequest()))
                .BuildAsync());

//#if (fluentValidation)
        app.MapPost("/items/create", async (ItemUpsertRequest request, IValidator<ItemUpsertRequest> validator) =>
        {
            var result = await validator.ValidateAsync(request);
            if (!result.IsValid)
            {
                request.Errors = result.ToErrorDictionary();
                return await Result.For(_CreatePage.Create(request))
                    .AsFragment(_CreatePage.Create(request))
                    .WithRetarget("#items-create-form")
                    .WithReswap(HtmxSwap.OuterHtml)
                    .BuildAsync();
            }

            _content.Create(request);
            return await Result.For(_Empty.Create())
                .WithTrigger("Items.ListRefresh")
                .WithToast("Item created successfully.")
                .BuildAsync();
        }).DisableAntiforgery();
//#endif
//#if (!fluentValidation)
        app.MapPost("/items/create", (ItemUpsertRequest request) =>
        {
            _content.Create(request);
            return Result.For(_Empty.Create())
                .WithTrigger("Items.ListRefresh")
                .WithToast("Item created successfully.")
                .BuildAsync();
        }).DisableAntiforgery();
//#endif

        // ─── EDIT ───

        app.MapGet("/items/edit/{id:int}", (int id) =>
        {
            var request = _content.GetForEdit(id);
            if (request is null)
                return Result.For(_Empty.Create())
                    .WithToast("Item not found.", tone: ToastTone.Error)
                    .BuildAsync();

            return Result.For(_EditPage.Create(request))
                .BuildAsync();
        });

//#if (fluentValidation)
        app.MapPost("/items/edit", async (ItemUpsertRequest request, IValidator<ItemUpsertRequest> validator) =>
        {
            var result = await validator.ValidateAsync(request);
            if (!result.IsValid)
            {
                request.Errors = result.ToErrorDictionary();
                return await Result.For(_EditPage.Create(request))
                    .AsFragment(_EditPage.Create(request))
                    .WithRetarget("#items-edit-form")
                    .WithReswap(HtmxSwap.OuterHtml)
                    .BuildAsync();
            }

            if (!_content.Update(request))
                return await Result.For(_Empty.Create())
                    .WithToast("Item not found.", tone: ToastTone.Error)
                    .BuildAsync();

            return await Result.For(_Empty.Create())
                .WithTrigger("Items.ListRefresh")
                .WithToast("Item updated successfully.")
                .BuildAsync();
        }).DisableAntiforgery();
//#endif
//#if (!fluentValidation)
        app.MapPost("/items/edit", (ItemUpsertRequest request) =>
        {
            if (!_content.Update(request))
                return Result.For(_Empty.Create())
                    .WithToast("Item not found.", tone: ToastTone.Error)
                    .BuildAsync();

            return Result.For(_Empty.Create())
                .WithTrigger("Items.ListRefresh")
                .WithToast("Item updated successfully.")
                .BuildAsync();
        }).DisableAntiforgery();
//#endif

        // ─── DELETE ───

        app.MapGet("/items/delete/{id:int}", (int id) =>
        {
            var model = _content.GetForDelete(id);
            if (model is null)
                return Result.For(_Empty.Create())
                    .WithToast("Item not found.", tone: ToastTone.Error)
                    .BuildAsync();

            return Result.For(_Empty.Create())
                .WithDialog(_DeleteDialog.Create(model))
                .BuildAsync();
        });

        app.MapPost("/items/delete/{id:int}", (int id) =>
        {
            if (!_content.Delete(id))
                return Result.For(_Empty.Create())
                    .WithToast("Item not found.", tone: ToastTone.Error)
                    .BuildAsync();

            return Result.For(_Empty.Create())
                .WithTrigger("Items.ListRefresh")
                .WithToast("Item deleted.", tone: ToastTone.Warning)
                .BuildAsync();
        }).DisableAntiforgery();
    }
}
