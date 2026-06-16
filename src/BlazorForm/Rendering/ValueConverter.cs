using System.Globalization;
using BlazorForm.Core.Schema;

namespace BlazorForm.Rendering;

/// <summary>
/// Converts between the string values produced by HTML inputs and the CLR types expected by the model.
/// Conversions are culture-invariant to keep round-tripping deterministic.
/// </summary>
public static class ValueConverter
{
    /// <summary>Renders a stored value as the string an input element should display.</summary>
    public static string ToInputString(object? value, FieldType type)
    {
        if (value is null) return string.Empty;
        return value switch
        {
            DateTime dt => type == FieldType.Date ? dt.ToString("yyyy-MM-dd")
                : type == FieldType.Time ? dt.ToString("HH:mm")
                : dt.ToString("yyyy-MM-ddTHH:mm"),
            DateOnly d => d.ToString("yyyy-MM-dd"),
            TimeOnly t => t.ToString("HH:mm"),
            DateTimeOffset dto => dto.ToString("yyyy-MM-ddTHH:mm"),
            double db => db.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString(CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            _ => value.ToString() ?? string.Empty
        };
    }

    /// <summary>Parses an input string into the target CLR type (falling back to the field type).</summary>
    public static object? FromInputString(string? raw, Type? targetType, FieldType fieldType)
    {
        var type = targetType is null ? null : Nullable.GetUnderlyingType(targetType) ?? targetType;
        var nullable = targetType is not null && Nullable.GetUnderlyingType(targetType) is not null;

        if (string.IsNullOrEmpty(raw))
            return type is null || nullable || type == typeof(string) || !type.IsValueType ? null : Activator.CreateInstance(type);

        // No target type: infer from field type.
        if (type is null)
        {
            return fieldType switch
            {
                FieldType.Integer => long.TryParse(raw, out var l) ? l : raw,
                FieldType.Number or FieldType.Range => double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : raw,
                FieldType.Checkbox => raw is "true" or "on" or "True",
                _ => raw
            };
        }

        try
        {
            if (type == typeof(string)) return raw;
            if (type.IsEnum) return Enum.Parse(type, raw, ignoreCase: true);
            if (type == typeof(bool)) return raw is "true" or "on" or "True";
            if (type == typeof(DateTime)) return DateTime.Parse(raw, CultureInfo.InvariantCulture);
            if (type == typeof(DateTimeOffset)) return DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture);
            if (type == typeof(DateOnly)) return DateOnly.Parse(raw, CultureInfo.InvariantCulture);
            if (type == typeof(TimeOnly)) return TimeOnly.Parse(raw, CultureInfo.InvariantCulture);
            if (type == typeof(Guid)) return Guid.Parse(raw);
            return Convert.ChangeType(raw, type, CultureInfo.InvariantCulture);
        }
        catch
        {
            return raw; // keep raw input so the user can correct it; validation will flag it
        }
    }
}
