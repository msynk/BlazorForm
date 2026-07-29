using System.Globalization;

namespace BlazorForm;

/// <summary>Enforces an inclusive numeric range.</summary>
public sealed class BlazorFormRangeRule(double? min, double? max, string? message = null) : IBlazorFormValidationRule
{
    /// <inheritdoc />
    public string Key => "range";

    /// <summary>The inclusive lower bound, if any.</summary>
    public double? Min => min;

    /// <summary>The inclusive upper bound, if any.</summary>
    public double? Max => max;

    public ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext ctx)
    {
        if (ctx.Value is null) return new(BlazorFormRuleResult.Success());
        if (!BlazorFormNumber.TryToDouble(ctx.Value, out var d)) return new(BlazorFormRuleResult.Success());

        if ((min.HasValue && d < min.Value) || (max.HasValue && d > max.Value))
            return new(BlazorFormRuleResult.Fail(message ?? RangeMessage(ctx)));
        return new(BlazorFormRuleResult.Success());
    }

    private string RangeMessage(BlazorFormValidationContext ctx) => (min, max) switch
    {
        ({ } lo, { } hi) => ctx.Message(BlazorFormMessageKeys.RangeBetween, Format(lo), Format(hi)),
        ({ } lo, null) => ctx.Message(BlazorFormMessageKeys.RangeMin, Format(lo)),
        (null, { } hi) => ctx.Message(BlazorFormMessageKeys.RangeMax, Format(hi)),
        _ => ctx.Message(BlazorFormMessageKeys.RangeBetween)
    };

    // Bounds are shown to the user, so they follow the ambient culture rather than the invariant
    // formatting used for HTML attributes.
    private static string Format(double value) => value.ToString("0.############", CultureInfo.CurrentCulture);
}
