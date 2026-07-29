using System.Linq.Expressions;
using System.Reflection;

namespace BlazorForm;

/// <summary>
/// Turns a member-access lambda into the dotted data path BlazorForm binds by:
/// <c>x =&gt; x.Address.City</c> becomes <c>Address.City</c>. Shared by the typed builder, the step
/// builder and anywhere else a refactor-safe field reference is useful.
/// </summary>
public static class BlazorFormExpressionPath
{
    /// <summary>Returns the dotted path of the member the expression selects.</summary>
    /// <exception cref="ArgumentException">The expression is not a chain of property accesses.</exception>
    public static string Of<TModel, TValue>(Expression<Func<TModel, TValue>> selector)
        => Resolve(selector).Path;

    /// <summary>Returns the dotted path and the member it ends at.</summary>
    public static (string Path, MemberInfo Member) Resolve<TModel, TValue>(Expression<Func<TModel, TValue>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        Expression body = selector.Body;
        var parts = new List<string>();
        MemberInfo? leaf = null;

        while (true)
        {
            // `x => x.Age` typed as Func<T, object?> arrives wrapped in a boxing conversion.
            if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            {
                body = unary.Operand;
                continue;
            }

            if (body is MemberExpression member)
            {
                leaf ??= member.Member;
                parts.Add(member.Member.Name);
                body = member.Expression!;
                continue;
            }

            if (body is ParameterExpression) break;

            throw new ArgumentException(
                "Field selector must be a property access such as x => x.Name or x => x.Address.City.",
                nameof(selector));
        }

        if (leaf is null || parts.Count == 0)
            throw new ArgumentException("Field selector must select a property.", nameof(selector));

        parts.Reverse();
        return (string.Join('.', parts), leaf);
    }
}
