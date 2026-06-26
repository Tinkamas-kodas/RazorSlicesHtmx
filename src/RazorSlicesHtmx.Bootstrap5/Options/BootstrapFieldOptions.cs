namespace RazorSlicesHtmx.Bootstrap5.Options;

/// <summary>
/// CSS options for Bootstrap 5 form field helpers.
/// Use <c>BootstrapFieldOptions.Default with { ... }</c> to override specific values.
/// Configure globally via <c>AddRazorSlicesHtmxBootstrap5</c> or pass per-call.
/// </summary>
public record BootstrapFieldOptions(
    string LabelClass,
    string InputClass,
    string InputErrorClass,
    string TextAreaClass,
    string TextAreaErrorClass,
    string SelectClass,
    string SelectErrorClass,
    string CheckboxClass,
    string CheckboxWrapperClass,
    string CheckboxLabelClass,
    string ValidationClass)
{
    public static readonly BootstrapFieldOptions Default = new(
        LabelClass: "form-label",
        InputClass: "form-control",
        InputErrorClass: "form-control is-invalid",
        TextAreaClass: "form-control",
        TextAreaErrorClass: "form-control is-invalid",
        SelectClass: "form-select",
        SelectErrorClass: "form-select is-invalid",
        CheckboxClass: "form-check-input",
        CheckboxWrapperClass: "form-check",
        CheckboxLabelClass: "form-check-label",
        ValidationClass: "invalid-feedback");

    /// <summary>
    /// Global singleton used by field helpers when no explicit options are passed.
    /// Set via <c>AddRazorSlicesHtmxBootstrap5</c> or directly at startup.
    /// </summary>
    public static BootstrapFieldOptions Current { get; set; } = Default;
}
