using System.Globalization;

namespace BlazorForm;

/// <summary>
/// Enforces an inclusive range. Bounds are held as numbers; a date, time or timestamp is compared as
/// the OLE automation number that stands for it, which is the same form the schema's
/// <see cref="BlazorFormFieldDefinition.Min"/>/<see cref="BlazorFormFieldDefinition.Max"/> and the
/// rendered <c>min</c>/<c>max</c> attributes already use.
/// </summary>
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

        // A date is not a number, but the bound it is judged against is stored as one — so a value the
        // numeric widening rejects gets a second chance as a date before the rule gives up. Without
        // that, `[Range(typeof(DateTime), "2020-01-01", "2030-01-01")]` was accepted by the scanner,
        // rendered as the input's min/max, and then never enforced at all: every date passed.
        if (!BlazorFormNumber.TryToDouble(ctx.Value, out var d)
            && !BlazorFormNumber.TryToOADate(ctx.Value, out d))
            return new(BlazorFormRuleResult.Success());

        if ((min.HasValue && d < min.Value) || (max.HasValue && d > max.Value))
            return new(BlazorFormRuleResult.Fail(message ?? RangeMessage(ctx)));
        return new(BlazorFormRuleResult.Success());
    }

    private string RangeMessage(BlazorFormValidationContext ctx)
    {
        var type = ctx.Field?.Type;
        return (min, max) switch
        {
            ({ } lo, { } hi) => ctx.Message(BlazorFormMessageKeys.RangeBetween, Format(lo, type), Format(hi, type)),
            ({ } lo, null) => ctx.Message(BlazorFormMessageKeys.RangeMin, Format(lo, type)),
            (null, { } hi) => ctx.Message(BlazorFormMessageKeys.RangeMax, Format(hi, type)),
            _ => ctx.Message(BlazorFormMessageKeys.RangeBetween)
        };
    }

    /// <summary>
    /// Renders a bound the way the user reads the field. "45292" is not a date anyone can act on, so on
    /// a date field the number is turned back into the date it stands for; everywhere else it is a
    /// number and stays one. Bounds are shown to the user, so they follow the ambient culture rather
    /// than the invariant formatting used for HTML attributes.
    /// </summary>
    private static string Format(double value, BlazorFormFieldType? type) => type switch
    {
        BlazorFormFieldType.Date => FromOADate(value)?.ToString("d", CultureInfo.CurrentCulture) ?? Number(value),
        BlazorFormFieldType.Time => FromOADate(value)?.ToString("t", CultureInfo.CurrentCulture) ?? Number(value),
        BlazorFormFieldType.DateTime => FromOADate(value)?.ToString("g", CultureInfo.CurrentCulture) ?? Number(value),
        _ => Number(value)
    };

    private static DateTime? FromOADate(double value)
    {
        try
        {
            return DateTime.FromOADate(value);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string Number(double value) => value.ToString("0.############", CultureInfo.CurrentCulture);
}
