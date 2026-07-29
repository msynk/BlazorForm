using System.Collections;

namespace BlazorForm;

/// <summary>
/// Enforces JSON Schema's <c>uniqueItems</c> on an array field. Items are compared by their
/// invariant string form so the rule works equally on a dictionary-backed form and a typed model.
/// </summary>
public sealed class BlazorFormUniqueItemsRule(string? message = null) : IBlazorFormValidationRule
{
    /// <inheritdoc />
    public string Key => "uniqueItems";

    public ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext ctx)
    {
        if (ctx.Value is not IEnumerable items || ctx.Value is string)
            return new(BlazorFormRuleResult.Success());

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var key = BlazorFormValueConverter.ToInvariantString(item);
            if (!seen.Add(key))
                return new(BlazorFormRuleResult.Fail(message ?? ctx.Message(BlazorFormMessageKeys.UniqueItems)));
        }
        return new(BlazorFormRuleResult.Success());
    }
}
