namespace razr_slices_htmx2.Features.Form.Models;

public sealed record FormPreviewRequest(
    string? Name,
    string? Team,
    string? Intent,
    string? Notes);