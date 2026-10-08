namespace RazorSlicesHtmx.AspNetCore.Models;

/// <summary>
/// A single lookup option value/text pair.
/// </summary>
public readonly record struct LookupItem(string Value, string Text);
