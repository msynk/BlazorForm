namespace BlazorForm;

/// <summary>Enforces a maximum string length.</summary>
public sealed class BlazorFormMaxLengthRule(int max, string? message = null) : IBlazorFormValidationRule
{
    /// <inheritdoc />
    public string Key => "maxLength";

    /// <summary>The configured maximum length.</summary>
    public int Max => max;

    public ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext ctx)
    {
        var s = ctx.Value as string;
        if (s is null || s.Length <= max)
            return new(BlazorFormRuleResult.Success());
        return new(BlazorFormRuleResult.Fail(message ?? ctx.Message(BlazorFormMessageKeys.MaxLength, max)));
    }
}
