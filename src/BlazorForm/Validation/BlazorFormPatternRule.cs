using System.Text.RegularExpressions;

namespace BlazorForm;

/// <summary>Validates against a regular expression.</summary>
public sealed class BlazorFormPatternRule(string pattern, string? message = null) : IBlazorFormValidationRule
{
    private readonly Regex _regex = new(pattern, RegexOptions.Compiled);

    public ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext ctx)
    {
        var s = ctx.Value as string;
        if (string.IsNullOrEmpty(s) || _regex.IsMatch(s))
            return new(BlazorFormRuleResult.Success());
        return new(BlazorFormRuleResult.Fail(message ?? "Invalid format."));
    }
}
