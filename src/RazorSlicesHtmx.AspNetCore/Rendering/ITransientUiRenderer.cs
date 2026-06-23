using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Models;

namespace RazorSlicesHtmx.AspNetCore.Rendering;

public interface ITransientUiRenderer
{
    RazorSlice RenderDialog(RazorSlice content);

    RazorSlice RenderToast(ToastModel toast);
}