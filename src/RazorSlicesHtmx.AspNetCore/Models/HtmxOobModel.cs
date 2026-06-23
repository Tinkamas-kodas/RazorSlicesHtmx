using RazorSlices;

namespace RazorSlicesHtmx.AspNetCore.Models;

public sealed record HtmxOobModel(
    string Selector,
    string Swap,
    RazorSlice Content);