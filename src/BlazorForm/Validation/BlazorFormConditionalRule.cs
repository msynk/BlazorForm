namespace BlazorForm;

/// <summary>
/// Wraps another rule so it only runs when <paramref name="condition"/> holds.
/// Used for conditional validation (e.g. "required when country == US").
/// </summary>
public sealed class BlazorFormConditionalRule(IBlazorFormValidationRule inner, Func<BlazorFormValidationContext, bool> condition) : IBlazorFormValidationRule
{
    public bool IsAsync => inner.IsAsync;

    public ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext ctx)
        => condition(ctx) ? inner.ValidateAsync(ctx) : new(BlazorFormRuleResult.Success());
}
