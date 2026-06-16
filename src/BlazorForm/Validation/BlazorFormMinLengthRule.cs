namespace BlazorForm;

/// <summary>Enforces a minimum string length.</summary>
public sealed class BlazorFormMinLengthRule(int min, string? message = null) : IBlazorFormValidationRule
{
    public ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext ctx)
    {
        var s = ctx.Value as string;
        if (string.IsNullOrEmpty(s) || s.Length >= min)
            return new(BlazorFormRuleResult.Success());
        return new(BlazorFormRuleResult.Fail(message ?? $"Must be at least {min} characters."));
    }
}
