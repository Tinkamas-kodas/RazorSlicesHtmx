namespace RazorSlicesHtmx.AspNetCore.Models;

public sealed record ToastModel(
    string Title,
    string Message,
    string Tone);