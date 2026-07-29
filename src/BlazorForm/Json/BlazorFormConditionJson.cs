using System.Text.Json;

namespace BlazorForm;

/// <summary>
/// Reads and writes the serialisable conditions (<see cref="BlazorFormFieldCondition"/> and
/// <see cref="BlazorFormConditionGroup"/>) used by the <c>x-visibleWhen</c>, <c>x-disabledWhen</c> and
/// <c>x-requiredWhen</c> schema extensions. JSON Schema has no vocabulary for "show this field when
/// that one says X", so BlazorForm carries it in a small, explicit extension instead of trying to
/// encode UI intent in <c>if</c>/<c>then</c>.
/// </summary>
/// <remarks>
/// A single condition is <c>{ "field": "Country", "op": "Equals", "value": "US" }</c>; a group is
/// <c>{ "all": [ … ] }</c> or <c>{ "any": [ … ] }</c>. Conditions backed by a delegate
/// (<see cref="BlazorFormPredicateCondition"/>) cannot be represented and are skipped on export.
/// </remarks>
public static class BlazorFormConditionJson
{
    /// <summary>Parses a condition from its JSON form, returning null when the element is not a condition.</summary>
    public static IBlazorFormCondition? Read(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;

        if (element.TryGetProperty("all", out var all) && all.ValueKind == JsonValueKind.Array)
            return new BlazorFormConditionGroup(BlazorFormConditionLogic.And, ReadMany(all));

        if (element.TryGetProperty("any", out var any) && any.ValueKind == JsonValueKind.Array)
            return new BlazorFormConditionGroup(BlazorFormConditionLogic.Or, ReadMany(any));

        if (!element.TryGetProperty("field", out var field) || field.ValueKind != JsonValueKind.String)
            return null;

        var op = element.TryGetProperty("op", out var opElement) && opElement.ValueKind == JsonValueKind.String
                 && Enum.TryParse<BlazorFormConditionOperator>(opElement.GetString(), ignoreCase: true, out var parsed)
            ? parsed
            : BlazorFormConditionOperator.Equals;

        var value = element.TryGetProperty("value", out var valueElement) ? ReadValue(valueElement) : null;

        return new BlazorFormFieldCondition(field.GetString()!, op, value);
    }

    /// <summary>
    /// Writes a condition. Returns false — writing nothing — when the condition cannot be represented
    /// in JSON, so the caller can decide whether to omit the property entirely.
    /// </summary>
    public static bool TryWrite(Utf8JsonWriter writer, IBlazorFormCondition condition)
    {
        switch (condition)
        {
            case BlazorFormFieldCondition field:
                writer.WriteStartObject();
                writer.WriteString("field", field.FieldPath);
                writer.WriteString("op", field.Operator.ToString());
                if (field.Value is not null)
                {
                    writer.WritePropertyName("value");
                    WriteValue(writer, field.Value);
                }
                writer.WriteEndObject();
                return true;

            case BlazorFormConditionGroup group:
                // A group with an unrepresentable child would silently change meaning, so it is
                // dropped as a whole rather than exported half-complete.
                var children = group.Conditions.Where(CanWrite).ToList();
                if (children.Count != group.Conditions.Count) return false;

                writer.WriteStartObject();
                writer.WritePropertyName(group.Logic == BlazorFormConditionLogic.And ? "all" : "any");
                writer.WriteStartArray();
                foreach (var child in children) TryWrite(writer, child);
                writer.WriteEndArray();
                writer.WriteEndObject();
                return true;

            default:
                return false;
        }
    }

    /// <summary>Whether <paramref name="condition"/> can be represented in JSON at all.</summary>
    public static bool CanWrite(IBlazorFormCondition? condition) => condition switch
    {
        null => false,
        BlazorFormFieldCondition => true,
        BlazorFormConditionGroup group => group.Conditions.All(CanWrite),
        _ => false
    };

    /// <summary>Writes <paramref name="condition"/> under <paramref name="propertyName"/> when it is representable.</summary>
    public static void WriteIfRepresentable(Utf8JsonWriter writer, string propertyName, IBlazorFormCondition? condition)
    {
        if (!CanWrite(condition)) return;
        writer.WritePropertyName(propertyName);
        TryWrite(writer, condition!);
    }

    private static IBlazorFormCondition[] ReadMany(JsonElement array)
        => array.EnumerateArray().Select(Read).OfType<IBlazorFormCondition>().ToArray();

    internal static object? ReadValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Array => element.EnumerateArray().Select(ReadValue).ToList(),
        _ => null
    };

    internal static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null: writer.WriteNullValue(); break;
            case bool b: writer.WriteBooleanValue(b); break;
            case int i: writer.WriteNumberValue(i); break;
            case long l: writer.WriteNumberValue(l); break;
            case double d: writer.WriteNumberValue(d); break;
            case float f: writer.WriteNumberValue(f); break;
            case decimal m: writer.WriteNumberValue(m); break;
            case string s: writer.WriteStringValue(s); break;
            case System.Collections.IEnumerable e:
                writer.WriteStartArray();
                foreach (var item in e) WriteValue(writer, item);
                writer.WriteEndArray();
                break;
            default:
                writer.WriteStringValue(BlazorFormValueConverter.ToInvariantString(value));
                break;
        }
    }
}
