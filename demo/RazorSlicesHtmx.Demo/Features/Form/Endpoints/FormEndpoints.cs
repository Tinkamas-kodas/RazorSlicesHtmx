using Microsoft.AspNetCore.Mvc;
using razr_slices_htmx2.Features.Form.Models;
using razr_slices_htmx2.Features.Form.Services;
using razr_slices_htmx2.Features.Form.Slices;
using razr_slices_htmx2.Shared.Models;

namespace razr_slices_htmx2.Features.Form.Endpoints;

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