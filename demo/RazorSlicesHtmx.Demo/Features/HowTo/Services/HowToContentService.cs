using RazorSlicesHtmx.Demo.Features.HowTo.Slices;
using RazorSlices;

namespace RazorSlicesHtmx.Demo.Features.HowTo.Services;

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

    public NavigationRouteItem CreateNavigationItem() => new(
        "howto",
        "HowTo",
        "/howto",
        new PageDefinition(_HowToPage.Create),
        Order: 400);

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