namespace BlazorForm;

/// <summary>A synchronous custom rule backed by a delegate.</summary>
public sealed class BlazorFormDelegateRule(Func<BlazorFormValidationContext, BlazorFormRuleResult> validate) : IBlazorFormValidationRule
{
    public ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext ctx) => new(validate(ctx));
}
