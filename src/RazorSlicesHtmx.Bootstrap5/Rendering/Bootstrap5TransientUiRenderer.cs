using Microsoft.Extensions.Options;
using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Options;
using RazorSlicesHtmx.AspNetCore.Rendering;
using RazorSlicesHtmx.Bootstrap5.Slices;

namespace RazorSlicesHtmx.Bootstrap5.Rendering;

public sealed class Bootstrap5TransientUiRenderer(IOptions<RazorSlicesHtmxOptions> options) : ITransientUiRenderer
{
    private readonly RazorSlicesHtmxOptions _options = options.Value;

    public RazorSlice RenderDialog(RazorSlice content) => _Dialog.Create(new DialogModel(content, _options.DialogClass ?? string.Empty));

    public RazorSlice RenderToast(ToastModel toast) => _Toast.Create(toast);
}