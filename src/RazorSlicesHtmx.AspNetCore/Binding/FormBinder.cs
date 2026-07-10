namespace RazorSlicesHtmx.AspNetCore.Binding;

using Microsoft.AspNetCore.Http;

public static class FormBinder
{
    public static async ValueTask<T> BindAsync<T>(HttpContext context, Func<T> factory)
    {
        var model = factory();

        return model;
    }

    public static ValueTask<T> BindAsync<T>(HttpContext context)
        where T : new()
        => BindAsync(context, static () => new T());
}