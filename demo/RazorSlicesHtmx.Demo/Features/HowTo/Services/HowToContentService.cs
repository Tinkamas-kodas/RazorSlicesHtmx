using RazorSlicesHtmx.Demo.Features.HowTo.Slices;
using RazorSlices;

namespace RazorSlicesHtmx.Demo.Features.HowTo.Services;

public sealed class HowToContentService
{


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