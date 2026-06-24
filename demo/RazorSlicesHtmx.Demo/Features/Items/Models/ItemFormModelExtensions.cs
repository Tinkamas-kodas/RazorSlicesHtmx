using System.Linq.Expressions;

namespace RazorSlicesHtmx.Demo.Shared.Models;

public static class ModelFieldExpressionExtensions
{
    public static string PathFor<TModel, TValue>(this TModel _, Expression<Func<TModel, TValue>> expression)
    {
        return GetMemberPath(expression.Body);
    }

    public static string NameFor<TModel, TValue>(this TModel model, Expression<Func<TModel, TValue>> expression)
    {
        var path = model.PathFor(expression);
        var separator = path.LastIndexOf('.');

        return separator >= 0 ? path[(separator + 1)..] : path;
    }

    public static string IdFor<TModel, TValue>(this TModel model, Expression<Func<TModel, TValue>> expression)
    {
        var name = $"{typeof(TModel).Name}-{model.PathFor(expression)}";

        return name.Replace('.', '-').ToLowerInvariant();
    }

    private static string GetMemberPath(Expression expression)
    {
        if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
        {
            return GetMemberPath(unary.Operand);
        }

        if (expression is not MemberExpression member)
        {
            throw new ArgumentException("Expression must be a member access.", nameof(expression));
        }

        var segments = new Stack<string>();
        Expression? current = member;

        while (current is MemberExpression currentMember)
        {
            segments.Push(currentMember.Member.Name);
            current = currentMember.Expression;
        }

        return string.Join('.', segments);
    }
}