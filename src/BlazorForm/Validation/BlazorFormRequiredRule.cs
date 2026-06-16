using System.Collections;

namespace BlazorForm;

/// <summary>Fails when the value is null, empty or whitespace (or an empty collection).</summary>
public sealed class BlazorFormRequiredRule(string? message = null) : IBlazorFormValidationRule
{
    public ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext ctx)
    {
        var empty = ctx.Value switch
        {
            null => true,
            string s => string.IsNullOrWhiteSpace(s),
            IEnumerable e and not string => !e.Cast<object?>().Any(),
            _ => false
        };
        return new(empty ? BlazorFormRuleResult.Fail(message ?? "This field is required.") : BlazorFormRuleResult.Success());
    }
}
