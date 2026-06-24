using RazorSlicesHtmx.Demo.Features.Form.Models;
using RazorSlicesHtmx.Demo.Features.Form.Slices;

namespace RazorSlicesHtmx.Demo.Features.Form.Services;

public sealed class FormContentService
{
    public FeatureMetadata CreateMetadata() => new(
        300,
        "form",
        "Form",
        "/form",
        "HTMX preview interaction");

    public PageDefinition CreatePageDefinition() => new(
        CreateMetadata(),
        () => _FormDetail.Create(CreateDetailModel()));

    public FormDetailModel CreateDetailModel() => new(
        "This slice contains a real HTMX form. The shared page shell still only swaps the details pane, while the form itself targets a nested preview region inside this page.",
        [
            "Admin navigation",
            "Fragment rendering",
            "OOB updates"
        ],
        "Submit the form to render a server-generated preview inside this nested panel.");

    public FormPreviewModel CreatePreviewModel(FormPreviewRequest request) => new(
        string.IsNullOrWhiteSpace(request.Name) ? "Unnamed request" : request.Name,
        string.IsNullOrWhiteSpace(request.Team) ? "Not provided" : request.Team,
        string.IsNullOrWhiteSpace(request.Intent) ? "Not provided" : request.Intent,
        string.IsNullOrWhiteSpace(request.Notes) ? "No notes submitted yet." : request.Notes);
}