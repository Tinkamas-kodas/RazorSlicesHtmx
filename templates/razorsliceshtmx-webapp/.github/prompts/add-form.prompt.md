---
mode: agent
description: "Add a create/edit form to an existing feature"
---

# Add a Form to a Feature

Add a create or edit form with HTMX submission to an existing feature module.

## Ask the user for:
1. **Feature name** — which existing feature to add the form to
2. **Entity name** — what is being created/edited (e.g., "Product")
3. **Fields** — form fields with types (e.g., "Name: string, Price: decimal, IsEnabled: bool")
4. **Action** — "create", "edit", or "both"

## Create these files:

### `Features/{FeatureName}/Models/{Entity}UpsertRequest.cs`
```csharp
using RazorSlicesHtmx.Generators;

namespace {RootNamespace}.Features.{FeatureName}.Models;

[GenerateHtmlNames]
public sealed class {Entity}UpsertRequest
{
    {properties with get; set; for each field}
}
```

### `Features/{FeatureName}/Slices/_CreatePage.cshtml` (or `_EditPage.cshtml`)
```razor
@inherits RazorSlice<{Entity}UpsertRequest>

@{
    var n = {Entity}UpsertRequestHtmlNames.Instance;
}

<form hx-post="{route}/create"
      hx-target="this"
      hx-swap="@HtmxSwap.OuterHtml">
    <div class="mb-3">
        <label for="@n.{Field}" class="form-label">{Field Label}</label>
        <input type="text" class="form-control" id="@n.{Field}" name="@n.{Field}" value="@Model.{Field}">
    </div>
    {repeat for each field}
    <button type="submit" class="btn btn-primary">Save</button>
</form>
```

## Update existing files:

### Update `{FeatureName}Endpoints.cs`
Add form endpoints:
```csharp
// Show create form
app.MapGet("{route}/create", () =>
    Result.For(_CreatePage.Create(new {Entity}UpsertRequest()))
        .BuildAsync());

// Handle create submission
app.MapPost("{route}/create", ({Entity}UpsertRequest request) =>
{
    // TODO: validate and save
    return Result.For(_Empty.Create())
        .WithTrigger("{FeatureName}.ListRefresh")
        .WithToast($"{Entity} created successfully.")
        .BuildAsync();
}).DisableAntiforgery();
```

## Important:
- `[GenerateHtmlNames]` generates a companion `{Entity}UpsertRequestHtmlNames` class at compile time
- Use `HtmlNames.Instance` for type-safe `name=""` attributes
- Always call `.DisableAntiforgery()` on POST endpoints (HTMX doesn't send antiforgery tokens by default)
- Use `.WithTrigger()` to refresh the list after successful form submission
- Use `.WithToast()` for user feedback
