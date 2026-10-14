namespace Apskaita.Feature.Admin.ProjectRegistry.Models;

public sealed record ProjectColorPreviewModel(
    string BackgroundColorRgb,
    string AccentColorRgb,
    string TextColorRgb,
    string ColorLabel,
    string ColorRgb,
    string SampleTime,
    string SampleDescription);