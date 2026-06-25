using System.Text.Json.Serialization;

namespace RazorSlicesHtmx.AspNetCore.Models;

/// <summary>
/// Semantic toast severity levels. UI framework packages map these
/// to their own visual styles (e.g. Bootstrap5 maps Error → "danger").
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ToastTone
{
    Info,
    Success,
    Warning,
    Error
}
