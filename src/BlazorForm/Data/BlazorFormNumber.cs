using System.Globalization;

namespace BlazorForm;

/// <summary>
/// Culture-safe numeric coercion shared by conditions, range validation and input attributes.
/// Boxed numeric types are widened directly rather than round-tripped through
/// <see cref="object.ToString()"/>, which would otherwise format with the ambient culture and then be
/// re-parsed as invariant — turning <c>1.5</c> into <c>15</c> under cultures that use a comma separator.
/// </summary>
internal static class BlazorFormNumber
{
    /// <summary>Attempts to widen <paramref name="value"/> to a <see cref="double"/>.</summary>
    public static bool TryToDouble(object? value, out double result)
    {
        switch (value)
        {
            case null:
                result = 0;
                return false;
            case double d: result = d; return true;
            case float f: result = f; return true;
            case decimal m: result = (double)m; return true;
            case int i: result = i; return true;
            case long l: result = l; return true;
            case short s: result = s; return true;
            case byte b: result = b; return true;
            case sbyte sb: result = sb; return true;
            case ushort us: result = us; return true;
            case uint ui: result = ui; return true;
            case ulong ul: result = ul; return true;
            case bool:
                result = 0;
                return false;
            case string str:
                return double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out result)
                    || double.TryParse(str, NumberStyles.Any, CultureInfo.CurrentCulture, out result);
            default:
                result = 0;
                return false;
        }
    }

    /// <summary>
    /// Widens a date, time or timestamp to the same OLE automation number the schema stores bounds as,
    /// so a date window can be checked by the one numeric range rule the rest of the library uses.
    /// </summary>
    /// <remarks>
    /// A time of day is the fraction of a day it represents — which is precisely its OLE automation
    /// value on day zero, so <c>09:00</c> compares equal whether it arrived as a <see cref="TimeOnly"/>,
    /// a <see cref="TimeSpan"/> or the string <c>"09:00"</c>. Parsing tries a time before a date for the
    /// same reason: <c>DateTime.Parse("09:00")</c> silently attaches <em>today</em> to it, which would
    /// put the bound and the value hundreds of thousands apart.
    /// </remarks>
    public static bool TryToOADate(object? value, out double result)
    {
        switch (value)
        {
            case DateTime dt: result = dt.ToOADate(); return true;
            case DateTimeOffset dto: result = dto.DateTime.ToOADate(); return true;
            case DateOnly d: result = d.ToDateTime(TimeOnly.MinValue).ToOADate(); return true;
            case TimeOnly t: result = t.ToTimeSpan().TotalDays; return true;
            case TimeSpan ts: result = ts.TotalDays; return true;
            case string s:
                if (TimeOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                {
                    result = time.ToTimeSpan().TotalDays;
                    return true;
                }
                if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                {
                    result = parsed.ToOADate();
                    return true;
                }
                result = 0;
                return false;
            default:
                result = 0;
                return false;
        }
    }

    /// <summary>Formats a number for an HTML attribute — always invariant, so <c>1.5</c> never becomes <c>1,5</c>.</summary>
    public static string? ToAttribute(double? value)
        => value?.ToString("R", CultureInfo.InvariantCulture);
}
