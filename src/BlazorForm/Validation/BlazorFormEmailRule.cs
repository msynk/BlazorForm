using System.Text.RegularExpressions;

namespace BlazorForm;

/// <summary>Validates an email address.</summary>
public sealed class BlazorFormEmailRule(string? message = null) : IBlazorFormValidationRule
{
    // Pragmatic email pattern; intentionally not RFC-perfect.
    private static readonly Regex Rx =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext ctx)
    {
        var s = ctx.Value as string;
        if (string.IsNullOrEmpty(s) || Rx.IsMatch(s))
            return new(BlazorFormRuleResult.Success());
        return new(BlazorFormRuleResult.Fail(message ?? "Enter a valid email address."));
    }
}
