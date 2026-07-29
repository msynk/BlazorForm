using System.Text.RegularExpressions;

namespace BlazorForm;

/// <summary>Validates an email address.</summary>
public sealed class BlazorFormEmailRule(string? message = null) : IBlazorFormValidationRule
{
    // Pragmatic email pattern; intentionally not RFC-perfect. Anchored and free of nested quantifiers,
    // so it cannot backtrack catastrophically on hostile input.
    private static readonly Regex Rx =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public string Key => "email";

    public ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext ctx)
    {
        var s = ctx.Value as string;
        if (string.IsNullOrEmpty(s) || Rx.IsMatch(s))
            return new(BlazorFormRuleResult.Success());
        return new(BlazorFormRuleResult.Fail(message ?? ctx.Message(BlazorFormMessageKeys.Email)));
    }
}
