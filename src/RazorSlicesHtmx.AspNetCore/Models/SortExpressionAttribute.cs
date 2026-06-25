namespace RazorSlicesHtmx.AspNetCore.Models;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class SortExpressionAttribute(string expression) : Attribute
{
    public string Expression { get; } = expression;
}