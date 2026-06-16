namespace BlazorForm;

/// <summary>An asynchronous custom rule backed by a delegate (e.g. remote uniqueness checks).</summary>
public sealed class BlazorFormAsyncDelegateRule(Func<BlazorFormValidationContext, ValueTask<BlazorFormRuleResult>> validate) : IBlazorFormValidationRule
{
    public bool IsAsync => true;
    public ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext ctx) => validate(ctx);
}
