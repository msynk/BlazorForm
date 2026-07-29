using System.Text.RegularExpressions;

namespace BlazorForm;

/// <summary>
/// Validates against a regular expression. Patterns that fail to compile are treated as "no
/// constraint" rather than throwing, so importing a schema with a malformed <c>pattern</c> keyword
/// still yields a usable form; see <see cref="IsPatternValid"/> to detect that case.
/// </summary>
public sealed class BlazorFormPatternRule(string pattern, string? message = null) : IBlazorFormValidationRule
{
    private readonly Regex? _regex = BlazorFormRegex.Create(pattern);

    /// <inheritdoc />
    public string Key => "pattern";

    /// <summary>The configured pattern.</summary>
    public string Pattern => pattern;

    /// <summary>False when <see cref="Pattern"/> is not a valid .NET regular expression.</summary>
    public bool IsPatternValid => _regex is not null;

    public ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext ctx)
    {
        var s = ctx.Value as string;
        if (string.IsNullOrEmpty(s)) return new(BlazorFormRuleResult.Success());

        return BlazorFormRegex.IsMatch(_regex, s) switch
        {
            true => new(BlazorFormRuleResult.Success()),
            false => new(BlazorFormRuleResult.Fail(message ?? ctx.Message(BlazorFormMessageKeys.Pattern))),
            null => new(BlazorFormRuleResult.Fail(ctx.Message(BlazorFormMessageKeys.PatternTimeout)))
        };
    }
}
