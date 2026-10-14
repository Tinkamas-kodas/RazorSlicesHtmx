using Apskaita.Shared.ProjectColors;

namespace Apskaita.Tests.Shared;

public sealed class ProjectColorCatalogTests
{
    [Theory]
    [InlineData("#F4B6C2", true)]
    [InlineData("#f4b6c2", true)]
    [InlineData("#FFF", false)]
    [InlineData("F4B6C2", false)]
    [InlineData("#GGGGGG", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsValidHex_MatchesSixDigitHash(string? value, bool expected) =>
        Assert.Equal(expected, ProjectColorCatalog.IsValidHex(value));

    [Fact]
    public void Normalize_Null_ReturnsDefault() =>
        Assert.Equal(ProjectColorCatalog.DefaultColorRgb, ProjectColorCatalog.Normalize(null));

    [Fact]
    public void Normalize_TrimsAndUppercases() =>
        Assert.Equal("#F4B6C2", ProjectColorCatalog.Normalize("  #f4b6c2  "));

    [Fact]
    public void NormalizeOrDefault_InvalidHex_ReturnsDefault() =>
        Assert.Equal(ProjectColorCatalog.DefaultColorRgb, ProjectColorCatalog.NormalizeOrDefault("#12345G"));

    [Fact]
    public void NormalizeOrDefault_ValidHex_ReturnsNormalized() =>
        Assert.Equal("#123456", ProjectColorCatalog.NormalizeOrDefault("#123456"));

    [Fact]
    public void Search_Empty_ReturnsAllOrderedBySortOrder()
    {
        var results = ProjectColorCatalog.Search(null);

        Assert.Equal(ProjectColorCatalog.Colors.Count, results.Count);
        Assert.Equal(ProjectColorCatalog.Colors[0].Name, results[0].Name);
    }

    [Fact]
    public void Search_Term_FiltersByName()
    {
        var results = ProjectColorCatalog.Search("rose");

        Assert.Contains(results, static c => c.Name == "Soft Rose");
        Assert.Contains(results, static c => c.Name == "Sand Rose");
        Assert.DoesNotContain(results, static c => c.Name == "Mint Wash");
    }

    [Fact]
    public void GetLookupText_KnownColor_ReturnsName() =>
        Assert.Equal("Soft Rose", ProjectColorCatalog.GetLookupText("#F4B6C2"));

    [Fact]
    public void GetLookupText_UnknownColor_ReturnsNormalized() =>
        Assert.Equal("#123456", ProjectColorCatalog.GetLookupText("#123456"));

    [Fact]
    public void CreatePresentation_LightColor_UsesDarkText()
    {
        var p = ProjectColorCatalog.CreatePresentation("#F4B6C2");

        Assert.Equal("#F4B6C2", p.BackgroundColorRgb);
        Assert.Equal("#111111", p.TextColorRgb);
    }

    [Fact]
    public void CreatePresentation_Black_UsesLightText() =>
        Assert.Equal("#FFFFFF", ProjectColorCatalog.CreatePresentation("#000000").TextColorRgb);

    [Fact]
    public void CreatePresentation_White_AccentIsDarkened() =>
        Assert.Equal("#D1D1D1", ProjectColorCatalog.CreatePresentation("#FFFFFF").AccentColorRgb);
}
