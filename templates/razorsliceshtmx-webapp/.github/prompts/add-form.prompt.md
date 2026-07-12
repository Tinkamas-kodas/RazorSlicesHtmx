---
agent: agent
description: "Add create, edit, and delete pages to an existing feature"
---

# Add CRUD Pages to a Feature

Add create form, edit form, and delete confirmation to an existing feature module.

## Ask the user for:
1. **Feature name** — which existing feature
2. **Entity name** — what is being created/edited/deleted (e.g., "Product")
3. **Fields** — form fields with types (e.g., "Name: string, Price: decimal, IsActive: bool")
4. **Which actions** — "create", "edit", "delete", or "all"

## Create these files:

### `Features/{FeatureName}/Models/{Entity}UpsertRequest.cs`

```csharp
namespace {RootNamespace}.Features.{FeatureName}.Models;

[GenerateHtmlNames]
public sealed class {Entity}UpsertRequest : IHaveValidationResult
{
    public int? Id { get; set; }
    {properties with get; set; for each field}

    public IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> Errors { get; set; } =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();
}
```

Note: `[GenerateHtmlNames]` is in namespace `RazorSlicesHtmx.HtmlNames` (already in GlobalUsings).
`IHaveValidationResult` carries validation errors back to the view.

### `Features/{FeatureName}/Models/{Entity}DeleteDialogModel.cs`

```csharp
namespace {RootNamespace}.Features.{FeatureName}.Models;

public sealed record {Entity}DeleteDialogModel(int Id, string Name);
```

### `Features/{FeatureName}/Slices/_{Entity}Form.cshtml`
**Reusable form fields** shared between create and edit pages:
```razor
@inherits RazorSlice<{Entity}UpsertRequest>
@using {RootNamespace}.HtmlNames.{RootNamespace}.Features.{FeatureName}.Models
@using RazorSlicesHtmx.Bootstrap5.Extensions

@{ var f = Model.For(Model.Errors); }

<input type="hidden" name="Id" value="@Model.Id">

<div>
    @f.Name.Label("Name")
    @f.Name.Input(placeholder: "Enter name", autoFocus: true)
    @f.Name.Validation()
</div>
@* repeat for each field *@

<div>
    @f.IsActive.Checkbox(label: "Active")
    <input type="hidden" name="@f.IsActive.Name" value="false">
</div>
```

Note: For bool checkboxes, always include a hidden input with `value="false"` after the checkbox.
This ensures the form submits `false` when the checkbox is unchecked.

### `Features/{FeatureName}/Slices/_CreatePage.cshtml`

```razor
@inherits RazorSlice<{Entity}UpsertRequest>

<article class="section-panel">
    <h3>Create {Entity}</h3>
    <p class="text-muted">Create a new {entity} and return to the list.</p>

    <div class="card border-0 shadow-sm">
        <div class="card-body p-4">
            <form class="d-flex flex-column gap-3"
                  hx-post
                  hx-target="#feature-details"
                  hx-push-url="false">
                @if (Model.Errors.Count > 0)
                {
                    <div class="alert alert-danger mb-0">Please fix the validation errors and submit again.</div>
                }

                @await RenderPartialAsync(_{Entity}Form.Create(Model))

                <div class="d-flex flex-wrap gap-2 pt-2">
                    <button type="submit" class="btn btn-dark">Create</button>
                    <button type="button" class="btn btn-outline-secondary"
                            hx-get="/{route}/list"
                            hx-target="#feature-details"
                            hx-include="#@{FeatureName}SearchModel.StateId, #@{FeatureName}ListQuery.StateId"
                            hx-push-url="true">Cancel</button>
                </div>
            </form>
        </div>
    </div>
</article>
```

### `Features/{FeatureName}/Slices/_EditPage.cshtml`
Same structure as create — uses the shared `_{Entity}Form`:
```razor
@inherits RazorSlice<{Entity}UpsertRequest>

<article class="section-panel">
    <h3>Edit {Entity}</h3>
    <p class="text-muted">Update the {entity} and return to the list.</p>

    <div class="card border-0 shadow-sm">
        <div class="card-body p-4">
            <form class="d-flex flex-column gap-3"
                  hx-post
                  hx-target="#feature-details"
                  hx-push-url="false">
                @if (Model.Errors.Count > 0)
                {
                    <div class="alert alert-danger mb-0">Please fix the validation errors and submit again.</div>
                }

                @await RenderPartialAsync(_{Entity}Form.Create(Model))

                <div class="d-flex flex-wrap gap-2 pt-2">
                    <button type="submit" class="btn btn-dark">Save changes</button>
                    <button type="button" class="btn btn-outline-secondary"
                            hx-get="/{route}/list"
                            hx-target="#feature-details"
                            hx-include="#@{FeatureName}SearchModel.StateId, #@{FeatureName}ListQuery.StateId"
                            hx-push-url="true">Cancel</button>
                </div>
            </form>
        </div>
    </div>
</article>
```

### `Features/{FeatureName}/Slices/_{Entity}Delete.cshtml`
Delete confirmation dialog — rendered inside Bootstrap modal via `Result.ShowDialog()`:
```razor
@inherits RazorSlice<{Entity}DeleteDialogModel>

<div class="modal-content border-0 shadow-lg">
    <form hx-post="@($"/{route}/delete/{Model.Id}")" hx-target="closest .modal-content" hx-swap="outerHTML">
        <div class="modal-header">
            <h5 class="modal-title">Delete {Entity}</h5>
            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
        </div>
        <div class="modal-body">
            <p>This will permanently delete <strong>@Model.Name</strong>.</p>
        </div>
        <div class="modal-footer">
            <button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal">Cancel</button>
            <button type="submit" class="btn btn-danger">Yes, delete</button>
        </div>
    </form>
</div>
```

### `Features/{FeatureName}/Validators/{Entity}UpsertRequestValidator.cs`
(Only when FluentValidation is enabled)
```csharp
using FluentValidation;
using {RootNamespace}.Features.{FeatureName}.Models;

namespace {RootNamespace}.Features.{FeatureName}.Validators;

public sealed class {Entity}UpsertRequestValidator : AbstractValidator<{Entity}UpsertRequest>
{
    public {Entity}UpsertRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must be 100 characters or fewer.");
        // add rules for other fields
    }
}
```

## Update existing files:

### Update `{FeatureName}ContentService.cs`
```csharp
public {Entity}UpsertRequest CreateCreateFormModel() => new()
{
    IsActive = true,
    Errors = EmptyErrors
};

public {Entity}UpsertRequest CreateEditFormModel({Entity}RowModel row) => new()
{
    Id = row.Id,
    Name = row.Name,
    // fill other fields from row
    Errors = EmptyErrors
};

public {Entity}DeleteDialogModel CreateDeleteDialogModel({Entity}RowModel row) =>
    new(row.Id, row.Name);

public void TrimRequest({Entity}UpsertRequest request)
{
    request.Name = request.Name?.Trim();
    // trim other string fields
}
```

### Update `{FeatureName}Endpoints.cs`

```csharp
// ─── CREATE ───

app.MapGet("/{route}/create", () =>
    Result.Page(_CreatePage.Create(_content.CreateCreateFormModel()))
        .WithPageTitle("Create {Entity}")
        .Build());

app.MapPost("/{route}/create", ({Entity}UpsertRequest request, IValidator<{Entity}UpsertRequest> validator) =>
    {
        _content.TrimRequest(request);
        var validationResult = validator.Validate(request);

        if (!validationResult.IsValid)
        {
            request.Errors = validationResult.ToErrorDictionary();
            return Result.Page(_CreatePage.Create(request))
                .Build();
        }

        _content.Create(request);
        return Result.Page(_Empty.Create())
            .WithTrigger("{FeatureName}.ListRefresh")
            .WithToast($"{Entity} '{request.Name}' was created.")
            .Build();
    });

// ─── EDIT ───

app.MapGet("/{route}/edit/{id:int}", (int id) =>
{
    var row = _content.FindById(id);
    if (row is null) return Task.FromResult(Results.NotFound());
    return Result.Page(_EditPage.Create(_content.CreateEditFormModel(row)))
    .WithPageTitle("Edit {Entity}")
        .Build();
});

app.MapPost("/{route}/edit/{id:int}", (int id, {Entity}UpsertRequest request, IValidator<{Entity}UpsertRequest> validator) =>
    {
        var row = _content.FindById(id);
        if (row is null) return Task.FromResult(Results.NotFound());

        request.Id = id;
        _content.TrimRequest(request);
        var validationResult = validator.Validate(request);

        if (!validationResult.IsValid)
        {
            request.Errors = validationResult.ToErrorDictionary();
            return Result.Page(_EditPage.Create(request))
                .Build();
        }

        _content.Apply(id, request);
        return Result.Page(_Empty.Create())
            .WithTrigger("{FeatureName}.ListRefresh")
            .WithToast($"{Entity} '{request.Name}' was updated.")
            .Build();
    });

// ─── DELETE ───

app.MapGet("/{route}/delete/{id:int}", (int id) =>
{
    var row = _content.FindById(id);
    if (row is null) return Results.NotFound();
    return Result.ShowDialog(_{Entity}Delete.Create(_content.CreateDeleteDialogModel(row)));
});

app.MapPost("/{route}/delete/{id:int}", (int id) =>
    {
        var row = _content.FindById(id);
        if (row is null) return Task.FromResult(Results.NotFound());

        _content.Delete(id);
        return Result.Page(_Empty.Create())
            .WithTrigger("{FeatureName}.ListRefresh")
            .WithToast($"{Entity} '{row.Name}' was deleted.", title: "Deleted", tone: ToastTone.Warning)
            .ClearDialog()
            .Build();
    });
```

## Key patterns:

### Validation error flow
1. Use `[FromForm]` to bind form data (not query string)
2. Call `_content.TrimRequest(request)` to normalize input
3. Validate with FluentValidation — `validator.Validate(request)`
4. On failure: set `request.Errors = validationResult.ToErrorDictionary()`, re-render the same page
5. `.For(Model.Errors)` creates field context with errors — helpers auto-add `is-invalid` CSS class
6. `.Validation()` renders error feedback per field

### Delete dialog flow
1. Table row has `hx-get="/{route}/delete/{id}"` targeting `#dialog-host`
2. Endpoint returns `Result.ShowDialog(content)` — wraps in Bootstrap modal markup
3. Modal contains a `<form>` with `hx-post` and `hx-target="closest .modal-content"`
4. Success returns `_Empty` + `.WithTrigger()` + `.WithToast()` + `.ClearDialog()`
5. `.ClearDialog()` removes the modal from `#dialog-host`

### Shared form pattern
- `_{Entity}Form.cshtml` contains only the form fields (no `<form>` tag, no buttons)
- Both `_CreatePage` and `_EditPage` render it via `@await RenderPartialAsync(_{Entity}Form.Create(Model))`
- This avoids duplicating field markup

## Important:
- `[GenerateHtmlNames]` creates `For.{Field}.Name` / `For.{Field}.Id` const strings
- Forms use `hx-post` (no explicit URL) — posts to the current URL
- For GET screens like create/edit/list, use `.WithPageTitle(...)` when you need a contextual title; without it, title defaults to `NavigationRouteItem.Label`
- Cancel buttons `hx-include` search/query state IDs to preserve list position
- All POST endpoints need `[FromForm]` and antiforgery validation via the shared HTMX token header adapter.
- Delete uses `Result.ShowDialog()` directly (not `.WithDialog()`)
- Delete POST uses `.ClearDialog()` to dismiss the modal
