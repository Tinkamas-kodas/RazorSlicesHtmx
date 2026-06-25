using Microsoft.AspNetCore.Html;
using RazorSlices;

namespace RazorSlicesHtmx.AspNetCore.Models;

public sealed record HtmxFragmentModel(
    RazorSlice Primary,
    IReadOnlyList<RazorSlice> OobParts,
    IReadOnlyList<IHtmlContent>? RawOobParts = null);