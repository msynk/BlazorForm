namespace BlazorForm;

/// <summary>
/// Enforces JSON Schema's <c>multipleOf</c>: the value must be an exact multiple of
/// <paramref name="factor"/>. Comparison uses a small epsilon so binary floating point
/// representation does not reject otherwise-valid input such as 0.3 against a step of 0.1.
/// </summary>
public sealed class BlazorFormMultipleOfRule(double factor, string? message = null) : IBlazorFormValidationRule
{
    /// <inheritdoc />
    public string Key => "multipleOf";

    public ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext ctx)
    {
        if (factor == 0 || ctx.Value is null) return new(BlazorFormRuleResult.Success());
        if (!BlazorFormNumber.TryToDouble(ctx.Value, out var d)) return new(BlazorFormRuleResult.Success());

        var quotient = d / factor;
        var ok = Math.Abs(quotient - Math.Round(quotient)) < 1e-9;

        return new(ok
            ? BlazorFormRuleResult.Success()
            : BlazorFormRuleResult.Fail(message ?? ctx.Message(BlazorFormMessageKeys.MultipleOf, factor)));
    }
}
