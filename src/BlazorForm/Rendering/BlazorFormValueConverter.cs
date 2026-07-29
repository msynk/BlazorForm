using System.Collections;
using System.Globalization;

namespace BlazorForm;

/// <summary>
/// Converts between the string values produced by HTML inputs and the CLR types expected by the model.
/// Conversions are culture-invariant to keep round-tripping deterministic: an <c>&lt;input type="date"&gt;</c>
/// always speaks ISO-8601 and an <c>&lt;input type="number"&gt;</c> always uses a dot decimal separator,
/// regardless of the server's current culture.
/// </summary>
public static class BlazorFormValueConverter
{
    /// <summary>Renders a stored value as the string an input element should display.</summary>
    public static string ToInputString(object? value, BlazorFormFieldType type)
    {
        if (value is null) return string.Empty;
        return value switch
        {
            string s => s,
            DateTime dt => type switch
            {
                BlazorFormFieldType.Date => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                BlazorFormFieldType.Time => dt.ToString("HH:mm", CultureInfo.InvariantCulture),
                _ => dt.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)
            },
            DateTimeOffset dto => type switch
            {
                BlazorFormFieldType.Date => dto.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                BlazorFormFieldType.Time => dto.ToString("HH:mm", CultureInfo.InvariantCulture),
                _ => dto.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)
            },
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeOnly t => t.ToString("HH:mm", CultureInfo.InvariantCulture),
            // <input type="time"> only understands HH:mm / HH:mm:ss, never "1.02:03:04".
            TimeSpan ts => ts.ToString(@"hh\:mm", CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            _ => ToInvariantString(value)
        };
    }

    /// <summary>
    /// Renders any value as a stable, culture-independent string — used for option matching,
    /// uniqueness checks and JSON export, where the ambient culture must not change the result.
    /// </summary>
    public static string ToInvariantString(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    /// <summary>Parses an input string into the target CLR type (falling back to the field type).</summary>
    public static object? FromInputString(string? raw, Type? targetType, BlazorFormFieldType fieldType)
    {
        var type = targetType is null ? null : Nullable.GetUnderlyingType(targetType) ?? targetType;
        var nullable = targetType is not null && Nullable.GetUnderlyingType(targetType) is not null;

        if (string.IsNullOrEmpty(raw))
        {
            if (type is null || nullable || !type.IsValueType) return null;
            // A non-nullable value type cannot hold "no value", so fall back to its default.
            return Activator.CreateInstance(type);
        }

        // No target type: infer from the field type (the dictionary-backed, schema-only case).
        if (type is null)
        {
            return fieldType switch
            {
                BlazorFormFieldType.Integer =>
                    long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : raw,
                BlazorFormFieldType.Number or BlazorFormFieldType.Range =>
                    double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : raw,
                BlazorFormFieldType.Checkbox => IsTruthy(raw),
                _ => raw
            };
        }

        return TryParse(raw, type, out var parsed)
            ? parsed
            : raw; // keep the raw input so the user can correct it; validation flags it
    }

    /// <summary>
    /// Coerces an arbitrary value to <paramref name="targetType"/>, handling nullables, enums,
    /// dates, and element-wise conversion of collections (so a <c>List&lt;string&gt;</c> gathered by a
    /// multi-select can be stored into a <c>List&lt;TEnum&gt;</c> property). Returns false when no safe
    /// conversion exists, letting callers leave the model untouched rather than throw.
    /// </summary>
    public static bool TryCoerce(object? value, Type targetType, out object? result)
    {
        result = null;
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (value is null)
        {
            // Reference and nullable targets accept null; a bare value type does not.
            if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) is not null) return true;
            result = Activator.CreateInstance(underlying);
            return true;
        }

        if (underlying.IsInstanceOfType(value))
        {
            result = value;
            return true;
        }

        if (value is string s)
        {
            if (s.Length == 0 && (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) is not null))
                return true;
            return TryParse(s, underlying, out result);
        }

        if (TryCoerceCollection(value, targetType, underlying, out result))
            return true;

        try
        {
            if (underlying.IsEnum)
            {
                result = Enum.ToObject(underlying, Convert.ChangeType(value, Enum.GetUnderlyingType(underlying), CultureInfo.InvariantCulture));
                return true;
            }
            if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(underlying))
            {
                result = Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
                return true;
            }
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
            // fall through
        }

        return false;
    }

    /// <summary>
    /// Materialises a new collection of <paramref name="targetType"/> holding the elements of
    /// <paramref name="value"/>, converting each one. Supports arrays and any type assignable
    /// from <c>List&lt;TElement&gt;</c> (<c>List&lt;T&gt;</c>, <c>IList&lt;T&gt;</c>, <c>ICollection&lt;T&gt;</c>,
    /// <c>IEnumerable&lt;T&gt;</c>).
    /// </summary>
    private static bool TryCoerceCollection(object value, Type targetType, Type underlying, out object? result)
    {
        result = null;
        if (value is not IEnumerable source || value is string) return false;

        var elementType = BlazorFormFieldTypeResolver.GetEnumerableElementType(underlying);
        if (elementType is null) return false;

        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;
        foreach (var item in source)
        {
            if (!TryCoerce(item, elementType, out var converted)) return false;
            list.Add(converted);
        }

        if (underlying.IsArray)
        {
            var array = Array.CreateInstance(elementType, list.Count);
            list.CopyTo(array, 0);
            result = array;
            return true;
        }

        if (!underlying.IsAssignableFrom(listType)) return false;
        result = list;
        return true;
    }

    /// <summary>Parses a string into <paramref name="type"/> using invariant, unambiguous formats.</summary>
    private static bool TryParse(string raw, Type type, out object? result)
    {
        result = null;
        try
        {
            if (type == typeof(string)) { result = raw; return true; }
            if (type.IsEnum) { result = Enum.Parse(type, raw, ignoreCase: true); return true; }
            if (type == typeof(bool)) { result = IsTruthy(raw); return true; }
            if (type == typeof(DateTime)) { result = DateTime.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None); return true; }
            if (type == typeof(DateTimeOffset)) { result = DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture); return true; }
            if (type == typeof(DateOnly)) { result = DateOnly.Parse(raw, CultureInfo.InvariantCulture); return true; }
            if (type == typeof(TimeOnly)) { result = TimeOnly.Parse(raw, CultureInfo.InvariantCulture); return true; }
            // <input type="time"> yields "HH:mm"; TimeSpan.Parse reads that as days when given "12", so be explicit.
            if (type == typeof(TimeSpan)) { result = ParseTimeSpan(raw); return result is not null; }
            if (type == typeof(Guid)) { result = Guid.Parse(raw); return true; }
            if (type == typeof(Uri)) { result = new Uri(raw, UriKind.RelativeOrAbsolute); return true; }
            if (type == typeof(char)) { result = raw.Length == 1 ? raw[0] : (object?)null; return result is not null; }

            result = Convert.ChangeType(raw, type, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException or UriFormatException)
        {
            result = null;
            return false;
        }
    }

    private static object? ParseTimeSpan(string raw)
        => TimeSpan.TryParseExact(raw, ["hh\\:mm", "hh\\:mm\\:ss", "c", "g", "G"], CultureInfo.InvariantCulture, out var ts)
            ? ts
            : TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out var loose) ? loose : null;

    private static bool IsTruthy(string raw)
        => raw.Equals("true", StringComparison.OrdinalIgnoreCase)
           || raw.Equals("on", StringComparison.OrdinalIgnoreCase)
           || raw == "1";
}
