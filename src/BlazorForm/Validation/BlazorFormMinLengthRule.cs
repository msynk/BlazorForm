using System.Collections;

namespace BlazorForm;

/// <summary>Enforces a minimum string length.</summary>
/// <remarks>
/// On a field that holds several strings rather than one — a tag list — the limit is applied to each
/// of them. An empty value is <see cref="BlazorFormRequiredRule"/>'s business either way.
/// </remarks>
public sealed class BlazorFormMinLengthRule(int min, string? message = null) : IBlazorFormValidationRule
{
    /// <inheritdoc />
    public string Key => "minLength";

    /// <summary>The configured minimum length.</summary>
    public int Min => min;

    public ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext ctx)
    {
        var tooShort = ctx.Value switch
        {
            string s => s.Length > 0 && s.Length < min,
            IEnumerable items when BlazorFormMaxLengthRule.IsMultiValued(ctx) => items.Cast<object?>()
                .Select(BlazorFormValueConverter.ToInvariantString)
                .Any(s => s.Length > 0 && s.Length < min),
            _ => false
        };

        return tooShort
            ? new(BlazorFormRuleResult.Fail(message ?? ctx.Message(BlazorFormMessageKeys.MinLength, min)))
            : new(BlazorFormRuleResult.Success());
    }
}
