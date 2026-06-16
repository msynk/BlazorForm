using System.Text.Json;

namespace BlazorForm;

/// <summary>
/// Builds a <see cref="BlazorFormDefinition"/> from a JSON Schema document (draft-07 style). Supports the
/// common keywords (type, properties, required, enum, format, min/max, length, pattern, items) plus a
/// few <c>x-</c> extensions (<c>x-widget</c>, <c>x-order</c>, <c>x-placeholder</c>, <c>enumNames</c>)
/// to express UI intent JSON Schema itself cannot.
/// </summary>
public static class BlazorFormJsonSchemaImporter
{
    public static BlazorFormDefinition Import(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return Import(doc.RootElement);
    }

    public static BlazorFormDefinition Import(JsonElement root)
    {
        var form = new BlazorFormDefinition();
        if (root.TryGetProperty("title", out var title)) form.Title = title.GetString();
        if (root.TryGetProperty("description", out var desc)) form.Description = desc.GetString();

        var required = ReadRequired(root);

        if (root.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in props.EnumerateObject())
            {
                var field = BuildField(prop.Name, prop.Value, required.Contains(prop.Name));
                form.Fields.Add(field);
            }
        }

        // Order fields by x-order when present.
        form.Fields = form.Fields.OrderBy(f => f.Order).ToList();
        return form;
    }

    private static BlazorFormFieldDefinition BuildField(string name, JsonElement schema, bool required)
    {
        var type = ReadString(schema, "type");
        var format = ReadString(schema, "format");
        var widget = ReadString(schema, "x-widget");

        var fieldType = MapType(type, format, schema);
        var field = new BlazorFormFieldDefinition(name, fieldType)
        {
            Label = ReadString(schema, "title") ?? BlazorFormFieldBuilder.Humanize(name),
            HelpText = ReadString(schema, "description"),
            Placeholder = ReadString(schema, "x-placeholder"),
            Required = required
        };

        if (schema.TryGetProperty("x-order", out var order) && order.TryGetInt32(out var o))
            field.Order = o;

        if (required)
            field.Validators.Add(new BlazorFormRequiredRule());

        ApplyWidget(field, widget);
        ApplyConstraints(field, schema);
        ApplyEnum(field, schema);

        if (schema.TryGetProperty("default", out var def))
            field.DefaultValue = ReadValue(def);

        // Composition
        if (fieldType == BlazorFormFieldType.Object && schema.TryGetProperty("properties", out var childProps))
        {
            var childRequired = ReadRequired(schema);
            foreach (var p in childProps.EnumerateObject())
                field.Children.Add(BuildField(p.Name, p.Value, childRequired.Contains(p.Name)));
        }
        else if (fieldType == BlazorFormFieldType.Array && schema.TryGetProperty("items", out var items))
        {
            var itemType = ReadString(items, "type");
            var itemFieldType = MapType(itemType, ReadString(items, "format"), items);
            if (itemFieldType == BlazorFormFieldType.Object && items.TryGetProperty("properties", out var itemProps))
            {
                var template = new BlazorFormFieldDefinition("item", BlazorFormFieldType.Object);
                var itemRequired = ReadRequired(items);
                foreach (var p in itemProps.EnumerateObject())
                    template.Children.Add(BuildField(p.Name, p.Value, itemRequired.Contains(p.Name)));
                field.ItemTemplate = template;
            }
            else
            {
                field.ItemTemplate = BuildField("item", items, false);
            }

            if (schema.TryGetProperty("minItems", out var mi) && mi.TryGetInt32(out var minI)) field.MinItems = minI;
            if (schema.TryGetProperty("maxItems", out var ma) && ma.TryGetInt32(out var maxI)) field.MaxItems = maxI;
            if (field.MinItems is not null || field.MaxItems is not null)
                field.Validators.Add(new BlazorFormCollectionSizeRule(field.MinItems, field.MaxItems));
        }

        return field;
    }

    private static BlazorFormFieldType MapType(string? type, string? format, JsonElement schema)
    {
        if (schema.TryGetProperty("enum", out _)) return BlazorFormFieldType.Select;

        return type switch
        {
            "integer" => BlazorFormFieldType.Integer,
            "number" => BlazorFormFieldType.Number,
            "boolean" => BlazorFormFieldType.Checkbox,
            "object" => BlazorFormFieldType.Object,
            "array" => BlazorFormFieldType.Array,
            "string" => format switch
            {
                "email" => BlazorFormFieldType.Email,
                "password" => BlazorFormFieldType.Password,
                "uri" or "url" => BlazorFormFieldType.Url,
                "date" => BlazorFormFieldType.Date,
                "date-time" => BlazorFormFieldType.DateTime,
                "time" => BlazorFormFieldType.Time,
                "color" => BlazorFormFieldType.Color,
                _ => BlazorFormFieldType.Text
            },
            _ => BlazorFormFieldType.Text
        };
    }

    private static void ApplyWidget(BlazorFormFieldDefinition field, string? widget)
    {
        if (string.IsNullOrEmpty(widget)) return;
        field.Type = widget switch
        {
            "textarea" => BlazorFormFieldType.TextArea,
            "radio" => BlazorFormFieldType.Radio,
            "multiselect" => BlazorFormFieldType.MultiSelect,
            "range" => BlazorFormFieldType.Range,
            "color" => BlazorFormFieldType.Color,
            "tel" => BlazorFormFieldType.Tel,
            "password" => BlazorFormFieldType.Password,
            "select" => BlazorFormFieldType.Select,
            _ => field.Type
        };
    }

    private static void ApplyConstraints(BlazorFormFieldDefinition field, JsonElement schema)
    {
        if (schema.TryGetProperty("minLength", out var minL) && minL.TryGetInt32(out var minLen))
        {
            field.MinLength = minLen;
            field.Validators.Add(new BlazorFormMinLengthRule(minLen));
        }
        if (schema.TryGetProperty("maxLength", out var maxL) && maxL.TryGetInt32(out var maxLen))
        {
            field.MaxLength = maxLen;
            field.Validators.Add(new BlazorFormMaxLengthRule(maxLen));
        }
        double? min = null, max = null;
        if (schema.TryGetProperty("minimum", out var mn) && mn.TryGetDouble(out var mnv)) min = mnv;
        if (schema.TryGetProperty("maximum", out var mx) && mx.TryGetDouble(out var mxv)) max = mxv;
        if (min is not null || max is not null)
        {
            field.Min = min;
            field.Max = max;
            field.Validators.Add(new BlazorFormRangeRule(min, max));
        }
        if (schema.TryGetProperty("pattern", out var pat) && pat.GetString() is { } pattern)
        {
            field.Pattern = pattern;
            field.Validators.Add(new BlazorFormPatternRule(pattern));
        }
    }

    private static void ApplyEnum(BlazorFormFieldDefinition field, JsonElement schema)
    {
        if (!schema.TryGetProperty("enum", out var en) || en.ValueKind != JsonValueKind.Array)
            return;

        string[]? names = null;
        if (schema.TryGetProperty("enumNames", out var enNames) && enNames.ValueKind == JsonValueKind.Array)
            names = enNames.EnumerateArray().Select(e => e.GetString() ?? "").ToArray();

        var i = 0;
        foreach (var item in en.EnumerateArray())
        {
            var value = item.ToString();
            var label = names is not null && i < names.Length ? names[i] : value;
            field.Options.Add(new BlazorFormSelectOption(value, label));
            i++;
        }

        if (field.Type is not (BlazorFormFieldType.Select or BlazorFormFieldType.MultiSelect or BlazorFormFieldType.Radio))
            field.Type = BlazorFormFieldType.Select;
    }

    private static HashSet<string> ReadRequired(JsonElement schema)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (schema.TryGetProperty("required", out var req) && req.ValueKind == JsonValueKind.Array)
            foreach (var r in req.EnumerateArray())
                if (r.GetString() is { } s) set.Add(s);
        return set;
    }

    private static string? ReadString(JsonElement element, string property)
        => element.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static object? ReadValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };
}
