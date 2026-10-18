using System.Text.RegularExpressions;

namespace BlazorForm;

/// <summary>Validates an email address.</summary>
public sealed class BlazorFormEmailRule(string? message = null) : IBlazorFormValidationRule
{
    // Pragmatic email pattern; intentionally not RFC-perfect. Anchored and free of nested quantifiers,
    // so it cannot backtrack catastrophically on hostile input — and given the same match timeout as
    // every schema pattern regardless, since "not catastrophic" is still quadratic on a long enough
    // string. The end anchor is \z rather than $: in .NET `$` also matches before a final newline, so
    // "a@b.co\n" passed.
    private static readonly Regex Rx =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+\z", RegexOptions.Compiled | RegexOptions.CultureInvariant, BlazorFormRegex.MatchTimeout);

    /// <inheritdoc />
    public string Key => "email";

    public ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext ctx)
    {
        var s = ctx.Value as string;
        if (string.IsNullOrEmpty(s)) return new(BlazorFormRuleResult.Success());

        return BlazorFormRegex.IsMatch(Rx, s) switch
        {
            true => new(BlazorFormRuleResult.Success()),
            false => new(BlazorFormRuleResult.Fail(message ?? ctx.Message(BlazorFormMessageKeys.Email))),
            null => new(BlazorFormRuleResult.Fail(ctx.Message(BlazorFormMessageKeys.PatternTimeout)))
        };
    }
}
