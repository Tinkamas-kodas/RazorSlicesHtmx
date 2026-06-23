using razr_slices_htmx2.Features.HowTo.Slices;
using RazorSlices;
using razr_slices_htmx2.Shared.Models;

namespace razr_slices_htmx2.Features.HowTo.Services;

public sealed class HowToContentService
{
    private static readonly string[] StepKeys =
    [
        "metadata",
        "models",
        "services",
        "slices",
        "endpoints",
        "discovery"
    ];

    public FeatureMetadata CreateMetadata() => new(
        400,
        "howto",
        "HowTo",
        "/howto",
        "How to add and register a new feature");

    public PageDefinition CreatePageDefinition() => new(
        CreateMetadata(),
        _HowToPage.Create);

    public bool TryCreateStepSlice(string? stepKey, out RazorSlice stepSlice)
    {
        switch (stepKey?.ToLowerInvariant())
        {
            case "metadata":
                stepSlice = Slice01.Create();
                return true;
            case "models":
                stepSlice = Slice02.Create();
                return true;
            case "services":
                stepSlice = Slice03.Create();
                return true;
            case "slices":
                stepSlice = Slice04.Create();
                return true;
            case "endpoints":
                stepSlice = Slice05.Create();
                return true;
            case "discovery":
                stepSlice = Slice06.Create();
                return true;
            default:
                stepSlice = Slice01.Create();
                return false;
        }
    }
}