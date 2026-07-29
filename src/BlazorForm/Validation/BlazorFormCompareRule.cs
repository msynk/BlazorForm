namespace BlazorForm;

/// <summary>
/// Requires the field to equal the value at another path — the "confirm password" / "confirm email"
/// rule. The other path is resolved against the whole form, so it works across nested objects.
/// </summary>
public sealed class BlazorFormCompareRule(string otherPath, string? otherLabel = null, string? message = null)
    : IBlazorFormValidationRule
{
    /// <inheritdoc />
    public string Key => "compare";

    /// <summary>The path of the field this one must match.</summary>
    public string OtherPath => otherPath;

    public ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext ctx)
    {
        var other = ctx.Data.GetValue(otherPath);
        var mine = ctx.Value;

        // An empty value is left to the required rule, so the two errors do not stack.
        if (mine is null || (mine is string s && s.Length == 0))
            return new(BlazorFormRuleResult.Success());

        var equal = mine is string a && other is string b
            ? string.Equals(a, b, StringComparison.Ordinal)
            : Equals(mine, other);

        return new(equal
            ? BlazorFormRuleResult.Success()
            : BlazorFormRuleResult.Fail(message ?? ctx.Message(BlazorFormMessageKeys.Compare, otherLabel ?? otherPath)));
    }
}
