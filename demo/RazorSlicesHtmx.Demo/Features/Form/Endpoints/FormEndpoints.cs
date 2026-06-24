using Microsoft.AspNetCore.Mvc;
using RazorSlicesHtmx.Demo.Features.Form.Models;
using RazorSlicesHtmx.Demo.Features.Form.Services;
using RazorSlicesHtmx.Demo.Features.Form.Slices;

namespace RazorSlicesHtmx.Demo.Features.Form.Endpoints;

public sealed class FormEndpoints : IFeatureModule
{
    private readonly FormContentService _content = new();

    public PageDefinition Page => _content.CreatePageDefinition();

    public void MapEndpoints(WebApplication app)
    {
        app.MapPost("/form/preview", ([FromForm] FormPreviewRequest request) =>
                _FormPreview.Create(_content.CreatePreviewModel(request)))
            .DisableAntiforgery();
    }
}