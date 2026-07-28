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
        new("form", "Form", "", Order: 300)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("", () =>
            Result.Page(_FormDetail.Create(_content.CreateDetailModel())));

        group.MapPost("preview", ([FromForm] FormPreviewRequest request) =>
                _FormPreview.Create(_content.CreatePreviewModel(request)))
            .DisableAntiforgery();
    }
}