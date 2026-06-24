namespace RazorSlicesHtmx.Demo.Features.Form.Models;

public sealed record FormPreviewRequest(
    string? Name,
    string? Team,
    string? Intent,
    string? Notes);