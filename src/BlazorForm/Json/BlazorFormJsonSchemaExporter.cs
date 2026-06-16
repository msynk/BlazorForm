using System.Text.Json;

namespace BlazorForm;

/// <summary>
/// Serialises a <see cref="BlazorFormDefinition"/> to a JSON Schema document (with <c>x-</c> extensions for
/// UI intent), enabling forms to be stored, transmitted and re-imported via <see cref="BlazorFormJsonSchemaImporter"/>.
/// </summary>
public static class BlazorFormJsonSchemaExporter
{
    public static string Export(BlazorFormDefinition form, bool indented = true)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented }))
            WriteObjectSchema(writer, form.Title, form.Description, form.Fields);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteObjectSchema(
        Utf8JsonWriter writer, string? title, string? description, IEnumerable<BlazorFormFieldDefinition> fields)
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

    private static void WriteField(Utf8JsonWriter writer, BlazorFormFieldDefinition field)
    {
        if (field.Type == BlazorFormFieldType.Object)
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

        if (field.Type == BlazorFormFieldType.Array && field.ItemTemplate is not null)
        {
            writer.WritePropertyName("items");
            if (field.ItemTemplate.Type == BlazorFormFieldType.Object)
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

    private static (string JsonType, string? Format, string? Widget) MapType(BlazorFormFieldType type) => type switch
    {
        BlazorFormFieldType.Integer => ("integer", null, null),
        BlazorFormFieldType.Number or BlazorFormFieldType.Range => ("number", null, type == BlazorFormFieldType.Range ? "range" : null),
        BlazorFormFieldType.Checkbox => ("boolean", null, null),
        BlazorFormFieldType.Array => ("array", null, null),
        BlazorFormFieldType.Email => ("string", "email", null),
        BlazorFormFieldType.Password => ("string", "password", null),
        BlazorFormFieldType.Url => ("string", "uri", null),
        BlazorFormFieldType.Date => ("string", "date", null),
        BlazorFormFieldType.DateTime => ("string", "date-time", null),
        BlazorFormFieldType.Time => ("string", "time", null),
        BlazorFormFieldType.Color => ("string", "color", null),
        BlazorFormFieldType.TextArea => ("string", null, "textarea"),
        BlazorFormFieldType.Radio => ("string", null, "radio"),
        BlazorFormFieldType.MultiSelect => ("array", null, "multiselect"),
        BlazorFormFieldType.Tel => ("string", null, "tel"),
        _ => ("string", null, null)
    };
}
