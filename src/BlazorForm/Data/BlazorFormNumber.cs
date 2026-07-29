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

    /// <summary>Formats a number for an HTML attribute — always invariant, so <c>1.5</c> never becomes <c>1,5</c>.</summary>
    public static string? ToAttribute(double? value)
        => value?.ToString("R", CultureInfo.InvariantCulture);
}
