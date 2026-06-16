using System.Text.Json;
using BlazorForm.Core.Schema;

namespace BlazorForm.Core.Json;

/// <summary>
/// Serialises a <see cref="FormDefinition"/> to a JSON Schema document (with <c>x-</c> extensions for
/// UI intent), enabling forms to be stored, transmitted and re-imported via <see cref="JsonSchemaImporter"/>.
/// </summary>
public static class JsonSchemaExporter
{
    public static string Export(FormDefinition form, bool indented = true)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented }))
            WriteObjectSchema(writer, form.Title, form.Description, form.Fields);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteObjectSchema(
        Utf8JsonWriter writer, string? title, string? description, IEnumerable<FieldDefinition> fields)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "object");
        if (title is not null) writer.WriteString("title", title);
        if (description is not null) writer.WriteString("description", description);

        var fieldList = fields.ToList();

        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        foreach (var field in fieldList)
        {
            writer.WritePropertyName(field.Name);
            WriteField(writer, field);
        }
        writer.WriteEndObject();

        var required = fieldList.Where(f => f.Required).Select(f => f.Name).ToList();
        if (required.Count > 0)
        {
            writer.WritePropertyName("required");
            writer.WriteStartArray();
            foreach (var r in required) writer.WriteStringValue(r);
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static void WriteField(Utf8JsonWriter writer, FieldDefinition field)
    {
        if (field.Type == FieldType.Object)
        {
            WriteObjectSchema(writer, field.Label, field.HelpText, field.Children);
            return;
        }

        writer.WriteStartObject();

        var (jsonType, format, widget) = MapType(field.Type);
        writer.WriteString("type", jsonType);
        if (format is not null) writer.WriteString("format", format);

        if (field.Label is not null) writer.WriteString("title", field.Label);
        if (field.HelpText is not null) writer.WriteString("description", field.HelpText);
        if (field.Placeholder is not null) writer.WriteString("x-placeholder", field.Placeholder);
        if (widget is not null) writer.WriteString("x-widget", widget);
        if (field.Order != 0) writer.WriteNumber("x-order", field.Order);

        if (field.MinLength is { } minL) writer.WriteNumber("minLength", minL);
        if (field.MaxLength is { } maxL) writer.WriteNumber("maxLength", maxL);
        if (field.Min is { } min) writer.WriteNumber("minimum", min);
        if (field.Max is { } max) writer.WriteNumber("maximum", max);
        if (field.Pattern is { } pattern) writer.WriteString("pattern", pattern);
        if (field.MinItems is { } minI) writer.WriteNumber("minItems", minI);
        if (field.MaxItems is { } maxI) writer.WriteNumber("maxItems", maxI);

        if (field.Options.Count > 0)
        {
            writer.WritePropertyName("enum");
            writer.WriteStartArray();
            foreach (var o in field.Options) writer.WriteStringValue(o.Value);
            writer.WriteEndArray();

            writer.WritePropertyName("enumNames");
            writer.WriteStartArray();
            foreach (var o in field.Options) writer.WriteStringValue(o.Label);
            writer.WriteEndArray();
        }

        if (field.DefaultValue is not null)
            WriteDefault(writer, field.DefaultValue);

        if (field.Type == FieldType.Array && field.ItemTemplate is not null)
        {
            writer.WritePropertyName("items");
            if (field.ItemTemplate.Type == FieldType.Object)
                WriteObjectSchema(writer, null, null, field.ItemTemplate.Children);
            else
                WriteField(writer, field.ItemTemplate);
        }

        writer.WriteEndObject();
    }

    private static void WriteDefault(Utf8JsonWriter writer, object value)
    {
        writer.WritePropertyName("default");
        switch (value)
        {
            case bool b: writer.WriteBooleanValue(b); break;
            case int i: writer.WriteNumberValue(i); break;
            case long l: writer.WriteNumberValue(l); break;
            case double d: writer.WriteNumberValue(d); break;
            case decimal m: writer.WriteNumberValue(m); break;
            default: writer.WriteStringValue(value.ToString()); break;
        }
    }

    private static (string JsonType, string? Format, string? Widget) MapType(FieldType type) => type switch
    {
        FieldType.Integer => ("integer", null, null),
        FieldType.Number or FieldType.Range => ("number", null, type == FieldType.Range ? "range" : null),
        FieldType.Checkbox => ("boolean", null, null),
        FieldType.Array => ("array", null, null),
        FieldType.Email => ("string", "email", null),
        FieldType.Password => ("string", "password", null),
        FieldType.Url => ("string", "uri", null),
        FieldType.Date => ("string", "date", null),
        FieldType.DateTime => ("string", "date-time", null),
        FieldType.Time => ("string", "time", null),
        FieldType.Color => ("string", "color", null),
        FieldType.TextArea => ("string", null, "textarea"),
        FieldType.Radio => ("string", null, "radio"),
        FieldType.MultiSelect => ("array", null, "multiselect"),
        FieldType.Tel => ("string", null, "tel"),
        _ => ("string", null, null)
    };
}
