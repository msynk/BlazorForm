using System.Collections;
using System.Text.RegularExpressions;

namespace BlazorForm;

/// <summary>
/// Validates against a regular expression. Patterns that fail to compile are treated as "no
/// constraint" rather than throwing, so importing a schema with a malformed <c>pattern</c> keyword
/// still yields a usable form; see <see cref="IsPatternValid"/> to detect that case.
/// </summary>
/// <remarks>
/// On a field that holds several strings rather than one — a tag list — the pattern is applied to each
/// of them, exactly as the length rules are. Reading the list as a string and finding it is not one
/// made the rule pass whatever the tags were.
/// </remarks>
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
        if (ctx.Value is IEnumerable items and not string && BlazorFormMaxLengthRule.IsMultiValued(ctx))
        {
            foreach (var item in items)
            {
                var text = BlazorFormValueConverter.ToInvariantString(item);
                if (text.Length == 0) continue;

                var result = Check(ctx, text);
                if (!result.IsValid) return new(result);
            }
            return new(BlazorFormRuleResult.Success());
        }

        var s = ctx.Value as string;
        if (string.IsNullOrEmpty(s)) return new(BlazorFormRuleResult.Success());

        return new(Check(ctx, s));
    }

    private BlazorFormRuleResult Check(BlazorFormValidationContext ctx, string text)
        => BlazorFormRegex.IsMatch(_regex, text) switch
        {
            true => BlazorFormRuleResult.Success(),
            false => BlazorFormRuleResult.Fail(message ?? ctx.Message(BlazorFormMessageKeys.Pattern)),
            null => BlazorFormRuleResult.Fail(ctx.Message(BlazorFormMessageKeys.PatternTimeout))
        };
}
