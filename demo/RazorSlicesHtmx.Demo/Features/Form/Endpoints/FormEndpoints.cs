using Microsoft.AspNetCore.Mvc;
using RazorSlicesHtmx.Demo.Features.Form.Models;
using RazorSlicesHtmx.Demo.Features.Form.Services;
using RazorSlicesHtmx.Demo.Features.Form.Slices;

namespace RazorSlicesHtmx.Demo.Features.Form.Endpoints;

public sealed class FormEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    private readonly FormContentService _content = new();

    public override IReadOnlyList<NavigationItem> NavigationItems =>
    [
        new("form", "Form", "/form", Order: 300)
    ];

    public override void MapEndpoints(WebApplication app)
    {
        app.MapGet("/form", () =>
            Result.For(_FormDetail.Create(_content.CreateDetailModel())).BuildAsync());

        app.MapPost("/form/preview", ([FromForm] FormPreviewRequest request) =>
                _FormPreview.Create(_content.CreatePreviewModel(request)))
            .DisableAntiforgery();
    }
}