using System.Globalization;

namespace BlazorForm;

/// <summary>Enforces an inclusive numeric range.</summary>
public sealed class BlazorFormRangeRule(double? min, double? max, string? message = null) : IBlazorFormValidationRule
{
    public ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext ctx)
    {
        if (ctx.Value is null) return new(BlazorFormRuleResult.Success());
        if (!TryToDouble(ctx.Value, out var d)) return new(BlazorFormRuleResult.Success());

        if (min.HasValue && d < min.Value)
            return new(BlazorFormRuleResult.Fail(message ?? RangeMessage()));
        if (max.HasValue && d > max.Value)
            return new(BlazorFormRuleResult.Fail(message ?? RangeMessage()));
        return new(BlazorFormRuleResult.Success());
    }

    private string RangeMessage() => (min, max) switch
    {
        ({ } lo, { } hi) => $"Must be between {lo} and {hi}.",
        ({ } lo, null) => $"Must be at least {lo}.",
        (null, { } hi) => $"Must be at most {hi}.",
        _ => "Out of range."
    };

    private static bool TryToDouble(object value, out double result)
        => double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
}
