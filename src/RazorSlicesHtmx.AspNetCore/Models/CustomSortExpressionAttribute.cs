namespace RazorSlicesHtmx.AspNetCore.Models;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class CustomSortExpressionAttribute(string methodName) : Attribute
{
    public string MethodName { get; } = methodName;
}