using RazorSlicesHtmx.AspNetCore.Html;

namespace RazorSlicesHtmx.Demo.Features.Items.Models;

[GenerateHtmlNames]
[GenerateFormBinder]
public sealed partial class ItemDeleteDialogModel:IHaveValidationResult
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; }="";
    public string EnteredCode { get; set; }="";
    public IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> Errors { get; set; } =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();
}