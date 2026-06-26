---
agent: agent
description: "Add create, edit, and delete pages to an existing feature"
---

# Add CRUD Pages to a Feature

Add create form, edit form, and delete confirmation to an existing feature module.

## Ask the user for:
1. **Feature name** — which existing feature
2. **Entity name** — what is being created/edited/deleted (e.g., "Product")
3. **Fields** — form fields with types (e.g., "Name: string, Price: decimal, IsEnabled: bool")
4. **Which actions** — "create", "edit", "delete", or "all"

## Create these files:

### `Features/{FeatureName}/Models/{Entity}UpsertRequest.cs`

```csharp
using RazorSlicesHtmx.Generators;

namespace {RootNamespace}.Features.{FeatureName}.Models;

[GenerateHtmlNames]
public sealed class {Entity}UpsertRequest : IHaveValidationResult
{
    public int? Id { get; set; }
    {properties with get; set; for each field}

    public Dictionary<string, string[]>? Errors { get; set; }
}
```

`IHaveValidationResult` allows the form to carry validation errors back to the view.

### `Features/{FeatureName}/Slices/_CreatePage.cshtml`

```razor
@inherits RazorSlice<{Entity}UpsertRequest>
@using For = {RootNamespace}.HtmlNames.{RootNamespace}.Features.{FeatureName}.Models.{Entity}UpsertRequestHtml

<form id="{featurename}-create-form"
      hx-post="/{route}/create"
      hx-target="this"
      hx-swap="@HtmxSwap.OuterHtml">

    <h3>Create {Entity}</h3>

    <div class="mb-3">
        <label for="@For.Name.Id" class="form-label">Name</label>
        <input type="text"
               class="form-control @InputClass(For.Name.Name, Model.Errors)"
               id="@For.Name.Id"
               name="@For.Name.Name"
               value="@Model.Name">
        @await RenderPartialAsync(_ValidationMessage.Create(new ValidationMessageModel(For.Name.Name, Model.Errors)))
    </div>
    @* repeat for each field *@

    <button type="submit" class="btn btn-primary">Create</button>
    <button type="button" class="btn btn-outline-secondary"
            hx-get="/{route}/list"
            hx-target="#feature-details"
            hx-push-url="true"
            hx-params="none">Cancel</button>
</form>
```

### `Features/{FeatureName}/Slices/_EditPage.cshtml`

Same structure as create but with `hx-post="/{route}/edit"` and pre-filled values:

```razor
@inherits RazorSlice<{Entity}UpsertRequest>
@using For = {RootNamespace}.HtmlNames.{RootNamespace}.Features.{FeatureName}.Models.{Entity}UpsertRequestHtml

<form id="{featurename}-edit-form"
      hx-post="/{route}/edit"
      hx-target="this"
      hx-swap="@HtmxSwap.OuterHtml">

    <h3>Edit {Entity}</h3>
    <input type="hidden" name="@For.Id.Name" value="@Model.Id">

    <div class="mb-3">
        <label for="@For.Name.Id" class="form-label">Name</label>
        <input type="text"
               class="form-control @InputClass(For.Name.Name, Model.Errors)"
               id="@For.Name.Id"
               name="@For.Name.Name"
               value="@Model.Name">
        @await RenderPartialAsync(_ValidationMessage.Create(new ValidationMessageModel(For.Name.Name, Model.Errors)))
    </div>
    @* repeat for each field *@

    <button type="submit" class="btn btn-primary">Save</button>
    <button type="button" class="btn btn-outline-secondary"
            hx-get="/{route}/list"
            hx-target="#feature-details"
            hx-push-url="true"
            hx-params="none">Cancel</button>
</form>
```

### `Features/{FeatureName}/Slices/_DeleteDialog.cshtml`

Delete uses a Bootstrap modal dialog:

```razor
@inherits RazorSlice<{Entity}DeleteModel>

<div class="modal-header">
    <h5 class="modal-title">Delete {Entity}</h5>
</div>
<div class="modal-body">
    <p>Are you sure you want to delete <strong>@Model.Name</strong>?</p>
</div>
<div class="modal-footer">
    <button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal">Cancel</button>
    <button type="button" class="btn btn-danger"
            hx-post="/{route}/delete/@Model.Id"
            hx-target="#feature-details"
            hx-params="none">Delete</button>
</div>
```

### `Features/{FeatureName}/Models/{Entity}DeleteModel.cs`

```csharp
namespace {RootNamespace}.Features.{FeatureName}.Models;

public sealed record {Entity}DeleteModel(int Id, string Name);
```

## Update existing files:

### Update `{FeatureName}Endpoints.cs`

```csharp
// ─── CREATE ───

app.MapGet("{route}/create", () =>
    Result.For(_CreatePage.Create(new {Entity}UpsertRequest()))
        .BuildAsync());

app.MapPost("{route}/create", async ({Entity}UpsertRequest request, IValidator<{Entity}UpsertRequest> validator) =>
{
    var result = await validator.ValidateAsync(request);
    if (!result.IsValid)
    {
        request.Errors = result.ToErrorDictionary();
        return await Result.For(_CreatePage.Create(request))
            .AsFragment(_CreatePage.Create(request))
            .WithRetarget("#{featurename}-create-form")
            .WithReswap(HtmxSwap.OuterHtml)
            .BuildAsync();
    }

    // TODO: save to database
    return await Result.For(_Empty.Create())
        .WithTrigger("{FeatureName}.ListRefresh")
        .WithToast("{Entity} created successfully.")
        .BuildAsync();
}).DisableAntiforgery();

// ─── EDIT ───

app.MapGet("{route}/edit/{id:int}", (int id) =>
{
    // TODO: load from database
    var request = new {Entity}UpsertRequest { Id = id, /* fill fields */ };
    return Result.For(_EditPage.Create(request))
        .BuildAsync();
});

app.MapPost("{route}/edit", async ({Entity}UpsertRequest request, IValidator<{Entity}UpsertRequest> validator) =>
{
    var result = await validator.ValidateAsync(request);
    if (!result.IsValid)
    {
        request.Errors = result.ToErrorDictionary();
        return await Result.For(_EditPage.Create(request))
            .AsFragment(_EditPage.Create(request))
            .WithRetarget("#{featurename}-edit-form")
            .WithReswap(HtmxSwap.OuterHtml)
            .BuildAsync();
    }

    // TODO: update in database
    return await Result.For(_Empty.Create())
        .WithTrigger("{FeatureName}.ListRefresh")
        .WithToast("{Entity} updated successfully.")
        .BuildAsync();
}).DisableAntiforgery();

// ─── DELETE ───

app.MapGet("{route}/delete/{id:int}", (int id) =>
{
    // TODO: load name from database
    var model = new {Entity}DeleteModel(id, "Item Name");
    return Result.For(Result.Dialog(_DeleteDialog.Create(model)))
        .BuildAsync();
});

app.MapPost("{route}/delete/{id:int}", (int id) =>
{
    // TODO: delete from database
    return Result.For(_Empty.Create())
        .WithTrigger("{FeatureName}.ListRefresh")
        .WithToast("{Entity} deleted.", tone: ToastTone.Warning)
        .BuildAsync();
}).DisableAntiforgery();
```

## Key patterns:

### Validation error flow
1. Validate with FluentValidation
2. On failure: set `request.Errors`, re-render the same form slice
3. Use `.WithRetarget("#form-id").WithReswap(HtmxSwap.OuterHtml)` to swap the form in-place
4. `@InputClass(For.Field.Name, Model.Errors)` adds `is-invalid` CSS class
5. `_ValidationMessage` renders Bootstrap validation feedback

### Delete dialog flow
1. Table row has `hx-get="/{route}/delete/{id}"` targeting `#dialog-host`
2. Endpoint returns `Result.Dialog(content)` — wraps in modal markup
3. Confirm button posts `hx-post="/{route}/delete/{id}"` with `hx-params="none"`
4. Success returns `_Empty` + `WithTrigger` (refreshes list) + `WithToast`

### Navigation after save
- Success responses use `.WithTrigger("{FeatureName}.ListRefresh")` to refresh the table
- The list's `hx-trigger="{FeatureName}.ListRefresh from:body"` catches this event
- Alternatively, redirect back to list: `hx-get="/{route}/list"` on a cancel button

## Important:
- `[GenerateHtmlNames]` creates `For.{Field}.Name` / `For.{Field}.Id` const strings
- `IHaveValidationResult` adds `Errors` property for validation feedback
- All POST endpoints need `.DisableAntiforgery()`
- Forms target themselves (`hx-target="this"`) for validation redisplay
- Delete uses `Result.Dialog()` → renders into `#dialog-host` as a Bootstrap modal
