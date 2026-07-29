using System.Text.Json;

namespace BlazorForm;

/// <summary>
/// Serialises a <see cref="BlazorFormDefinition"/> to a JSON Schema document (with <c>x-</c> extensions for
/// UI intent), enabling forms to be stored, transmitted and re-imported via <see cref="BlazorFormJsonSchemaImporter"/>.
/// </summary>
/// <remarks>
/// The export is designed to round-trip: conditions, wizard steps, layout and widget choices all
/// survive. The one thing that cannot is a condition or rule backed by a delegate
/// (<see cref="BlazorFormPredicateCondition"/>, <c>Must</c>/<c>MustAsync</c>) — code has no JSON form.
/// Such conditions are omitted rather than approximated.
/// </remarks>
public static class BlazorFormJsonSchemaExporter
{
    private const string SchemaVersion = "https://json-schema.org/draft/2020-12/schema";

    public static string Export(BlazorFormDefinition form, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(form);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented }))
            WriteRoot(writer, form);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteRoot(Utf8JsonWriter writer, BlazorFormDefinition form)
    {
        writer.WriteStartObject();
        writer.WriteString("$schema", SchemaVersion);
        WriteObjectBody(writer, form.Title, form.Description, form.Fields);

        if (form.Columns > 1) writer.WriteNumber("x-columns", form.Columns);
        WriteSteps(writer, form);

        writer.WriteEndObject();
    }

    private static void WriteObjectSchema(
        Utf8JsonWriter writer, string? title, string? description, IEnumerable<BlazorFormFieldDefinition> fields)
    {
        writer.WriteStartObject();
        WriteObjectBody(writer, title, description, fields);
        writer.WriteEndObject();
    }

    /// <summary>Writes the members of an object schema without opening or closing the object itself.</summary>
    private static void WriteObjectBody(
        Utf8JsonWriter writer, string? title, string? description, IEnumerable<BlazorFormFieldDefinition> fields)
    {
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
    }

    private static void WriteField(Utf8JsonWriter writer, BlazorFormFieldDefinition field)
    {
        writer.WriteStartObject();

        if (field.Type == BlazorFormFieldType.Object)
        {
            // An object field is an object schema plus the same UI metadata every other field carries.
            WriteObjectBody(writer, field.Label, field.HelpText, field.Children);
            WriteUiMetadata(writer, field);
            writer.WriteEndObject();
            return;
        }

        var (jsonType, format, widget) = MapType(field.Type);
        writer.WriteString("type", jsonType);
        if (format is not null) writer.WriteString("format", format);

        if (field.Label is not null) writer.WriteString("title", field.Label);
        if (field.HelpText is not null) writer.WriteString("description", field.HelpText);
        if (widget is not null) writer.WriteString("x-widget", widget);
        if (field.ReadOnly) writer.WriteBoolean("readOnly", true);

        if (field.MinLength is { } minL) writer.WriteNumber("minLength", minL);
        if (field.MaxLength is { } maxL) writer.WriteNumber("maxLength", maxL);
        if (field.Min is { } min) writer.WriteNumber("minimum", min);
        if (field.Max is { } max) writer.WriteNumber("maximum", max);

        // A UI step and a `multipleOf` constraint are different things: a price stepping by 0.01 in the
        // spinner is not a promise that 0.005 is invalid. Only a real MultipleOf rule exports as
        // multipleOf; the granularity of the control travels as x-step and re-imports as one.
        if (field.NumericStep is { } step)
        {
            if (field.Validators.Any(v => v.Key == "multipleOf")) writer.WriteNumber("multipleOf", step);
            else writer.WriteNumber("x-step", step);
        }

        if (field.Pattern is { } pattern) writer.WriteString("pattern", pattern);
        if (field.MinItems is { } minI) writer.WriteNumber("minItems", minI);
        if (field.MaxItems is { } maxI) writer.WriteNumber("maxItems", maxI);
        if (field.Validators.Any(v => v.Key == "uniqueItems")) writer.WriteBoolean("uniqueItems", true);

        if (field.Options.Count > 0)
        {
            writer.WritePropertyName("enum");
            writer.WriteStartArray();
            foreach (var o in field.Options) WriteOptionValue(writer, o.Value, jsonType);
            writer.WriteEndArray();

            writer.WritePropertyName("enumNames");
            writer.WriteStartArray();
            foreach (var o in field.Options) writer.WriteStringValue(o.Label);
            writer.WriteEndArray();
        }

        if (field.DefaultValue is not null)
        {
            writer.WritePropertyName("default");
            BlazorFormConditionJson.WriteValue(writer, field.DefaultValue);
        }

        if (field.Type == BlazorFormFieldType.Array && field.ItemTemplate is not null)
        {
            writer.WritePropertyName("items");
            if (field.ItemTemplate.Type == BlazorFormFieldType.Object)
                WriteObjectSchema(writer, null, null, field.ItemTemplate.Children);
            else
                WriteField(writer, field.ItemTemplate);
        }

        WriteUiMetadata(writer, field);
        writer.WriteEndObject();
    }

    /// <summary>Writes the <c>x-</c> extensions carrying intent JSON Schema itself cannot express.</summary>
    private static void WriteUiMetadata(Utf8JsonWriter writer, BlazorFormFieldDefinition field)
    {
        if (field.Placeholder is not null) writer.WriteString("x-placeholder", field.Placeholder);
        if (field.Order != 0) writer.WriteNumber("x-order", field.Order);
        if (field.Autocomplete is not null) writer.WriteString("x-autocomplete", field.Autocomplete);
        if (field.InputMode is not null) writer.WriteString("x-inputMode", field.InputMode);
        if (field.ColumnSpan is { } span) writer.WriteNumber("x-colSpan", span);
        if (field.Accept is not null) writer.WriteString("x-accept", field.Accept);
        if (field.Multiple) writer.WriteBoolean("x-multiple", true);
        if (field.ClearOnHide) writer.WriteBoolean("x-clearOnHide", true);
        if (field.Autofocus) writer.WriteBoolean("x-autofocus", true);
        if (field.MaxFileSize is { } maxFile) writer.WriteNumber("x-maxFileSize", maxFile);

        if (!field.ShowLabel) writer.WriteBoolean("x-showLabel", false);
        if (field.Prefix is not null) writer.WriteString("x-prefix", field.Prefix);
        if (field.Suffix is not null) writer.WriteString("x-suffix", field.Suffix);
        if (field.ShowCharacterCount) writer.WriteBoolean("x-characterCount", true);
        if (field.UpdateOn == BlazorFormUpdateTrigger.Input) writer.WriteString("x-updateOn", "input");
        if (field.DebounceMilliseconds > 0) writer.WriteNumber("x-debounce", field.DebounceMilliseconds);
        if (field.CustomRenderer is { Length: > 0 } renderer) writer.WriteString("x-renderer", renderer);

        // `examples` is JSON Schema's own vocabulary for "values like these", which is exactly what a
        // datalist offers — no x- prefix needed.
        if (field.Suggestions.Count > 0)
        {
            writer.WritePropertyName("examples");
            writer.WriteStartArray();
            foreach (var s in field.Suggestions) writer.WriteStringValue(s);
            writer.WriteEndArray();
        }

        WriteAttributes(writer, "x-attributes", field.Attributes);
        WriteAttributes(writer, "x-inputAttributes", field.InputAttributes);

        BlazorFormConditionJson.WriteIfRepresentable(writer, "x-visibleWhen", field.VisibleWhen);
        BlazorFormConditionJson.WriteIfRepresentable(writer, "x-disabledWhen", field.DisabledWhen);
        BlazorFormConditionJson.WriteIfRepresentable(writer, "x-requiredWhen", field.RequiredWhen);
    }

    /// <summary>
    /// Writes a renderer-hint bag. Values that have no JSON form (a delegate, a component type someone
    /// stashed there) are skipped rather than approximated, on the same principle as the conditions.
    /// </summary>
    private static void WriteAttributes(Utf8JsonWriter writer, string property, IDictionary<string, object?> attributes)
    {
        if (attributes.Count == 0) return;

        var writable = attributes.Where(a => IsJsonWritable(a.Value)).ToList();
        if (writable.Count == 0) return;

        writer.WritePropertyName(property);
        writer.WriteStartObject();
        foreach (var (key, value) in writable)
        {
            writer.WritePropertyName(key);
            BlazorFormConditionJson.WriteValue(writer, value);
        }
        writer.WriteEndObject();
    }

    private static bool IsJsonWritable(object? value)
        => value is null or string or bool or int or long or double or decimal or float or short or byte;

    private static void WriteSteps(Utf8JsonWriter writer, BlazorFormDefinition form)
    {
        if (!form.IsWizard) return;

        writer.WritePropertyName("x-steps");
        writer.WriteStartArray();
        foreach (var step in form.Steps)
        {
            writer.WriteStartObject();
            writer.WriteString("id", step.Id);
            if (step.Title is not null) writer.WriteString("title", step.Title);
            if (step.Description is not null) writer.WriteString("description", step.Description);

            writer.WritePropertyName("fields");
            writer.WriteStartArray();
            foreach (var f in step.Fields) writer.WriteStringValue(f);
            writer.WriteEndArray();

            BlazorFormConditionJson.WriteIfRepresentable(writer, "visibleWhen", step.VisibleWhen);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    /// <summary>
    /// Writes an option value using the field's own JSON type, so a numeric enum comes back as numbers
    /// rather than strings on re-import.
    /// </summary>
    private static void WriteOptionValue(Utf8JsonWriter writer, string value, string jsonType)
    {
        switch (jsonType)
        {
            case "integer" when long.TryParse(value, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var l):
                writer.WriteNumberValue(l);
                break;
            case "number" when double.TryParse(value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d):
                writer.WriteNumberValue(d);
                break;
            default:
                writer.WriteStringValue(value);
                break;
        }
    }

    private static (string JsonType, string? Format, string? Widget) MapType(BlazorFormFieldType type) => type switch
    {
        BlazorFormFieldType.Integer => ("integer", null, null),
        BlazorFormFieldType.Number => ("number", null, null),
        BlazorFormFieldType.Range => ("number", null, "range"),
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
        BlazorFormFieldType.File => ("string", "binary", "file"),
        BlazorFormFieldType.Hidden => ("string", null, "hidden"),
        BlazorFormFieldType.Static => ("null", null, "static"),
        BlazorFormFieldType.Custom => ("string", null, "custom"),
        _ => ("string", null, null)
    };
}
