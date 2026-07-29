using System.Collections;

namespace BlazorForm;

/// <summary>Fails when the value is null, empty or whitespace (or an empty collection).</summary>
public sealed class BlazorFormRequiredRule(string? message = null) : IBlazorFormValidationRule
{
    /// <inheritdoc />
    public string Key => "required";

    public ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext ctx)
    {
        var empty = ctx.Value switch
        {
            null => true,
            string s => string.IsNullOrWhiteSpace(s),
            // A required checkbox means "you must tick this" — the semantics of HTML's own `required`
            // on a checkbox, and what "I accept the terms" always needs. Elsewhere `false` is a value.
            bool b => !b && ctx.Field?.Type == BlazorFormFieldType.Checkbox,
            ICollection c => c.Count == 0,
            IEnumerable e and not string => !e.Cast<object?>().Any(),
            _ => false
        };
        return new(empty
            ? BlazorFormRuleResult.Fail(message ?? ctx.Message(BlazorFormMessageKeys.Required))
            : BlazorFormRuleResult.Success());
    }
}
