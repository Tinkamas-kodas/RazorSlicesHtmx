using System.Globalization;

namespace Apskaita.Shared.ProjectColors;

public static class ProjectColorCatalog
{
    public static IReadOnlyList<ProjectColorOption> Colors { get; } =
    [
        new("#F4B6C2", "Soft Rose", 1),
        new("#F7C6D0", "Blossom Pink", 2),
        new("#F8C8B8", "Peach Mist", 3),
        new("#F6D6B8", "Apricot Haze", 4),
        new("#F5E6A9", "Butter Cream", 5),
        new("#F2E7B4", "Lemon Veil", 6),
        new("#D8E2C8", "Sage Light", 7),
        new("#CFE5C2", "Meadow Mist", 8),
        new("#C6E8D5", "Mint Wash", 9),
        new("#BFE7DD", "Seafoam Soft", 10),
        new("#BEE7E8", "Aqua Hush", 11),
        new("#C7E3F5", "Sky Mist", 12),
        new("#D4E6F8", "Cloud Blue", 13),
        new("#D6D7F7", "Periwinkle Soft", 14),
        new("#E1D5F2", "Lavender Dust", 15),
        new("#E7D7F6", "Lilac Haze", 16),
        new("#E8D1E8", "Mauve Cloud", 17),
        new("#F2D7DF", "Blush Petal", 18),
        new("#E9D6CC", "Sand Rose", 19),
        new("#D5E1DA", "Stone Mint", 20)
    ];

    public static string DefaultColorRgb => Colors[0].Value;

    public static IReadOnlyList<ProjectColorOption> Search(string? query)
    {
        var term = query?.Trim();
        if (string.IsNullOrWhiteSpace(term))
        {
            return Colors;
        }

        return Colors
            .Where(color =>
                color.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                color.Value.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(color => color.SortOrder)
            .ToArray();
    }

    public static string Normalize(string? colorRgb)
    {
        if (string.IsNullOrWhiteSpace(colorRgb))
        {
            return DefaultColorRgb;
        }

        return colorRgb.Trim().ToUpperInvariant();
    }

    public static string NormalizeOrDefault(string? colorRgb)
    {
        var normalized = Normalize(colorRgb);
        return IsValidHex(normalized) ? normalized : DefaultColorRgb;
    }

    public static bool IsValidHex(string? colorRgb)
    {
        if (string.IsNullOrWhiteSpace(colorRgb))
        {
            return false;
        }

        var value = colorRgb.Trim();
        return value is ['#', _, _, _, _, _, _]
               && value.Skip(1).All(ch =>
                   ch is >= '0' and <= '9' ||
                   ch is >= 'A' and <= 'F' ||
                   ch is >= 'a' and <= 'f');
    }

    public static string GetLookupText(string? colorRgb)
    {
        var normalized = Normalize(colorRgb);
        var option = Colors.FirstOrDefault(color => color.Value.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        return option is null ? normalized : option.Name;
    }

    public static ProjectColorPresentation CreatePresentation(string? colorRgb)
    {
        var normalized = NormalizeOrDefault(colorRgb);
        if (!TryParseHexColor(normalized, out var r, out var g, out var b))
        {
            normalized = DefaultColorRgb;
            TryParseHexColor(normalized, out r, out g, out b);
        }

        var backgroundLuminance = CalculateRelativeLuminance(r, g, b);
        var darkContrast = CalculateContrastRatio(backgroundLuminance, CalculateRelativeLuminance(17, 17, 17));
        var lightContrast = CalculateContrastRatio(backgroundLuminance, CalculateRelativeLuminance(255, 255, 255));
        var textColor = darkContrast >= lightContrast ? "#111111" : "#FFFFFF";

        return new ProjectColorPresentation(
            normalized,
            textColor,
            Darken(normalized, 0.18d),
            GetLookupText(normalized));
    }

    private static string Darken(string colorRgb, double amount)
    {
        if (!TryParseHexColor(colorRgb, out var r, out var g, out var b))
        {
            return colorRgb;
        }

        var factor = Math.Clamp(1d - amount, 0d, 1d);
        return $"#{(int)Math.Round(r * factor):X2}{(int)Math.Round(g * factor):X2}{(int)Math.Round(b * factor):X2}";
    }

    private static bool TryParseHexColor(string hex, out int r, out int g, out int b)
    {
        r = 0;
        g = 0;
        b = 0;

        if (string.IsNullOrWhiteSpace(hex) || hex.Length != 7 || hex[0] != '#')
        {
            return false;
        }

        return
            int.TryParse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r) &&
            int.TryParse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g) &&
            int.TryParse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b);
    }

    private static double CalculateRelativeLuminance(int r, int g, int b)
    {
        static double Channel(double value)
        {
            var normalized = value / 255d;
            return normalized <= 0.03928d
                ? normalized / 12.92d
                : Math.Pow((normalized + 0.055d) / 1.055d, 2.4d);
        }

        return 0.2126d * Channel(r) + 0.7152d * Channel(g) + 0.0722d * Channel(b);
    }

    private static double CalculateContrastRatio(double luminanceA, double luminanceB)
    {
        var lighter = Math.Max(luminanceA, luminanceB);
        var darker = Math.Min(luminanceA, luminanceB);
        return (lighter + 0.05d) / (darker + 0.05d);
    }
}

public sealed record ProjectColorOption(string Value, string Name, int SortOrder);

public sealed record ProjectColorPresentation(
    string BackgroundColorRgb,
    string TextColorRgb,
    string AccentColorRgb,
    string Label);