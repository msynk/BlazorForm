using System.Collections;

namespace BlazorForm;

/// <summary>
/// Compares values for <see cref="BlazorFormFieldCondition"/>. Handles loose, culture-invariant
/// comparison so conditions authored in JSON (strings) work against typed model values.
/// </summary>
public static class BlazorFormConditionEvaluator
{
    public static bool Compare(object? actual, BlazorFormConditionOperator op, object? expected) => op switch
    {
        BlazorFormConditionOperator.IsEmpty => IsEmpty(actual),
        BlazorFormConditionOperator.IsNotEmpty => !IsEmpty(actual),
        BlazorFormConditionOperator.IsTrue => AsBool(actual) == true,
        BlazorFormConditionOperator.IsFalse => AsBool(actual) == false,
        BlazorFormConditionOperator.Equals => LooseEquals(actual, expected),
        BlazorFormConditionOperator.NotEquals => !LooseEquals(actual, expected),
        BlazorFormConditionOperator.GreaterThan => TryCompare(actual, expected, out var g) && g > 0,
        BlazorFormConditionOperator.GreaterThanOrEqual => TryCompare(actual, expected, out var ge) && ge >= 0,
        BlazorFormConditionOperator.LessThan => TryCompare(actual, expected, out var l) && l < 0,
        BlazorFormConditionOperator.LessThanOrEqual => TryCompare(actual, expected, out var le) && le <= 0,
        BlazorFormConditionOperator.Contains => Contains(actual, expected),
        BlazorFormConditionOperator.NotContains => !Contains(actual, expected),
        BlazorFormConditionOperator.In => In(actual, expected),
        BlazorFormConditionOperator.NotIn => !In(actual, expected),
        BlazorFormConditionOperator.StartsWith => StartsOrEnds(actual, expected, start: true),
        BlazorFormConditionOperator.EndsWith => StartsOrEnds(actual, expected, start: false),
        BlazorFormConditionOperator.Matches => Matches(actual, expected),
        _ => false
    };

    private static bool IsEmpty(object? value) => value switch
    {
        null => true,
        string s => string.IsNullOrWhiteSpace(s),
        ICollection c => c.Count == 0,
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

        // Numbers compare numerically (1 == 1.0 == "1"), everything else by invariant text.
        if (BlazorFormNumber.TryToDouble(a, out var da) && BlazorFormNumber.TryToDouble(b, out var db))
            return da.Equals(db);

        return string.Equals(
            BlazorFormValueConverter.ToInvariantString(a),
            BlazorFormValueConverter.ToInvariantString(b),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryCompare(object? a, object? b, out int result)
    {
        result = 0;
        if (a is null || b is null) return false;

        if (BlazorFormNumber.TryToDouble(a, out var da) && BlazorFormNumber.TryToDouble(b, out var db))
        {
            result = da.CompareTo(db);
            return true;
        }

        // Dates and other IComparable values: coerce the expected operand to the actual value's type
        // so a condition authored as the string "2024-01-01" still compares against a DateTime.
        if (a is IComparable ca)
        {
            if (a.GetType() == b.GetType())
            {
                result = ca.CompareTo(b);
                return true;
            }
            if (BlazorFormValueConverter.TryCoerce(b, a.GetType(), out var coerced) && coerced is not null)
            {
                result = ca.CompareTo(coerced);
                return true;
            }
        }
        return false;
    }

    private static bool Contains(object? actual, object? expected)
    {
        if (expected is null) return false;

        if (actual is string s)
            return s.Contains(BlazorFormValueConverter.ToInvariantString(expected), StringComparison.OrdinalIgnoreCase);

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

    private static bool StartsOrEnds(object? actual, object? expected, bool start)
    {
        if (actual is null || expected is null) return false;
        var haystack = BlazorFormValueConverter.ToInvariantString(actual);
        var needle = BlazorFormValueConverter.ToInvariantString(expected);
        return start
            ? haystack.StartsWith(needle, StringComparison.OrdinalIgnoreCase)
            : haystack.EndsWith(needle, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Matches(object? actual, object? expected)
    {
        if (actual is null || expected is null) return false;
        // Patterns can come from untrusted JSON: compiled with a timeout, and a pattern that neither
        // compiles nor completes simply does not match rather than taking the render down.
        var regex = BlazorFormRegex.Create(BlazorFormValueConverter.ToInvariantString(expected));
        if (regex is null) return false;
        return BlazorFormRegex.IsMatch(regex, BlazorFormValueConverter.ToInvariantString(actual)) == true;
    }
}
