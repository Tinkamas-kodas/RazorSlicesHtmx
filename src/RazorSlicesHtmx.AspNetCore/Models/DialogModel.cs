using RazorSlices;

namespace RazorSlicesHtmx.AspNetCore.Models;

public sealed record DialogModel(
    RazorSlice Content,
    string DialogClass = "");