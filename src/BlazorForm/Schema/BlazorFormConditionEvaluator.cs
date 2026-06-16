using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BlazorForm;

/// <summary>
/// Compares values for <see cref="BlazorFormFieldCondition"/>. Handles loose, culture-invariant
/// comparison so conditions authored in JSON (strings) work against typed model values.
/// </summary>
public static class BlazorFormConditionEvaluator
{
    public static bool Compare(object? actual, BlazorFormConditionOperator op, object? expected)
    {
        switch (op)
        {
            case BlazorFormConditionOperator.IsEmpty:
                return IsEmpty(actual);
            case BlazorFormConditionOperator.IsNotEmpty:
                return !IsEmpty(actual);
            case BlazorFormConditionOperator.IsTrue:
                return AsBool(actual) == true;
            case BlazorFormConditionOperator.IsFalse:
                return AsBool(actual) == false;
            case BlazorFormConditionOperator.Equals:
                return LooseEquals(actual, expected);
            case BlazorFormConditionOperator.NotEquals:
                return !LooseEquals(actual, expected);
            case BlazorFormConditionOperator.GreaterThan:
                return TryCompareNumeric(actual, expected, out var g) && g > 0;
            case BlazorFormConditionOperator.GreaterThanOrEqual:
                return TryCompareNumeric(actual, expected, out var ge) && ge >= 0;
            case BlazorFormConditionOperator.LessThan:
                return TryCompareNumeric(actual, expected, out var l) && l < 0;
            case BlazorFormConditionOperator.LessThanOrEqual:
                return TryCompareNumeric(actual, expected, out var le) && le <= 0;
            case BlazorFormConditionOperator.Contains:
                return Contains(actual, expected);
            case BlazorFormConditionOperator.NotContains:
                return !Contains(actual, expected);
            case BlazorFormConditionOperator.In:
                return In(actual, expected);
            case BlazorFormConditionOperator.NotIn:
                return !In(actual, expected);
            case BlazorFormConditionOperator.Matches:
                return expected is not null &&
                       actual is not null &&
                       Regex.IsMatch(actual.ToString() ?? string.Empty, expected.ToString() ?? string.Empty);
            default:
                return false;
        }
    }

    private static bool IsEmpty(object? value) => value switch
    {
        null => true,
        string s => string.IsNullOrWhiteSpace(s),
        IEnumerable e and not string => !e.Cast<object?>().Any(),
        _ => false
    };

    private static bool? AsBool(object? value) => value switch
    {
        bool b => b,
        string s when bool.TryParse(s, out var b) => b,
        _ => null
    };

    private static bool LooseEquals(object? a, object? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        if (a.Equals(b)) return true;

        if (TryToDouble(a, out var da) && TryToDouble(b, out var db))
            return da.Equals(db);

        return string.Equals(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryCompareNumeric(object? a, object? b, out int result)
    {
        result = 0;
        if (TryToDouble(a, out var da) && TryToDouble(b, out var db))
        {
            result = da.CompareTo(db);
            return true;
        }
        if (a is IComparable ca && b is not null && a.GetType() == b.GetType())
        {
            result = ca.CompareTo(b);
            return true;
        }
        return false;
    }

    private static bool Contains(object? actual, object? expected)
    {
        if (expected is null) return false;
        var needle = expected.ToString() ?? string.Empty;

        if (actual is string s)
            return s.Contains(needle, StringComparison.OrdinalIgnoreCase);

        if (actual is IEnumerable e and not string)
            return e.Cast<object?>().Any(x => LooseEquals(x, expected));

        return false;
    }

    private static bool In(object? actual, object? expected)
    {
        if (expected is IEnumerable e and not string)
            return e.Cast<object?>().Any(x => LooseEquals(actual, x));
        return LooseEquals(actual, expected);
    }

    private static bool TryToDouble(object? value, out double result)
    {
        switch (value)
        {
            case null:
                result = 0;
                return false;
            case double d:
                result = d;
                return true;
            case float f:
                result = f;
                return true;
            case int i:
                result = i;
                return true;
            case long l:
                result = l;
                return true;
            case decimal m:
                result = (double)m;
                return true;
            default:
                return double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }
    }
}
