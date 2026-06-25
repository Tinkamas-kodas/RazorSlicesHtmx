using System.Globalization;

namespace RazorSlicesHtmx.AspNetCore.Models;

public readonly record struct StateFieldValue(string Name, string Value)
{
    public static StateFieldValue FromObject(string name, object? value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("State field name is required.", nameof(name));
        }

        return new StateFieldValue(name, ToInvariantString(value));
    }

    private static string ToInvariantString(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is string s)
        {
            return s;
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return value.ToString() ?? string.Empty;
    }
}
