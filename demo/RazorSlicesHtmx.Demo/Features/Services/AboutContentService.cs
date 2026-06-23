using razr_slices_htmx2.Features.About.Models;
using razr_slices_htmx2.Features.Slices;
using razr_slices_htmx2.Shared.Models;

namespace razr_slices_htmx2.Features.About.Services;

public sealed class AboutContentService
{
    public FeatureMetadata CreateMetadata() => new(
        100,
        "about",
        "About",
        "/about",
        "Vertical architecture with RazorSlices and HTMX");

    public PageDefinition CreatePageDefinition() => new(
        CreateMetadata(),
        () => _AboutDetail.Create(CreateDetailModel()));

    public AboutDetailModel CreateDetailModel() => new(
        "This project uses a vertical-slice structure where each feature owns its endpoints, services, slices, and optional models. RazorSlices render the HTML on the server, while HTMX swaps only the parts of the page that need to change.",
        [
            "Features are grouped under Features/{FeatureName} so routes, rendering, and feature-specific logic stay together.",
            "Shared shell pieces live outside features: the layout, sidebar, shared section hero, and HTMX fragment helpers sit in Shared or Helpers.",
            "The root page router stays generic: it resolves a PageDefinition from the feature registry and renders either a full page or an HTMX fragment.",
            "Nested interactions, such as HowTo pills, Form preview updates, and the new Items CRUD dialogs, remain local to the owning feature instead of leaking into Program.cs."
        ],
        [
            "RazorSlices keep markup strongly typed without introducing MVC controllers or a separate component model.",
            "HTMX keeps navigation and partial updates simple: the server returns the next HTML fragment, and the browser swaps it into place.",
            "The new Items feature shows the full pattern end to end: EF Core InMemory storage, FluentValidation rules, modal create/edit/delete flows, and OOB toast feedback all fit inside one feature folder.",
            "The shared shell remains reusable because features provide concrete detail slices instead of a giant generic view model."
        ]);
}