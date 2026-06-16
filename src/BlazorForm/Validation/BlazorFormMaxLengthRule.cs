namespace BlazorForm;

/// <summary>Enforces a maximum string length.</summary>
public sealed class BlazorFormMaxLengthRule(int max, string? message = null) : IBlazorFormValidationRule
{
    public ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext ctx)
    {
        var s = ctx.Value as string;
        if (s is null || s.Length <= max)
            return new(BlazorFormRuleResult.Success());
        return new(BlazorFormRuleResult.Fail(message ?? $"Must be at most {max} characters."));
    }
}
