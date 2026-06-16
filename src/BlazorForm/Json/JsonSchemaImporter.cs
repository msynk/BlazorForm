using System.Text.Json;
using BlazorForm.Core.Building;
using BlazorForm.Core.Schema;
using BlazorForm.Core.Validation;

namespace BlazorForm.Core.Json;

/// <summary>
/// Builds a <see cref="FormDefinition"/> from a JSON Schema document (draft-07 style). Supports the
/// common keywords (type, properties, required, enum, format, min/max, length, pattern, items) plus a
/// few <c>x-</c> extensions (<c>x-widget</c>, <c>x-order</c>, <c>x-placeholder</c>, <c>enumNames</c>)
/// to express UI intent JSON Schema itself cannot.
/// </summary>
public static class JsonSchemaImporter
{
    public static FormDefinition Import(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return Import(doc.RootElement);
    }

    public static FormDefinition Import(JsonElement root)
    {
        var form = new FormDefinition();
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

    private static FieldDefinition BuildField(string name, JsonElement schema, bool required)
    {
        var type = ReadString(schema, "type");
        var format = ReadString(schema, "format");
        var widget = ReadString(schema, "x-widget");

        var fieldType = MapType(type, format, schema);
        var field = new FieldDefinition(name, fieldType)
        {
            Label = ReadString(schema, "title") ?? FieldBuilder.Humanize(name),
            HelpText = ReadString(schema, "description"),
            Placeholder = ReadString(schema, "x-placeholder"),
            Required = required
        };

        if (schema.TryGetProperty("x-order", out var order) && order.TryGetInt32(out var o))
            field.Order = o;

        if (required)
            field.Validators.Add(new RequiredRule());

        ApplyWidget(field, widget);
        ApplyConstraints(field, schema);
        ApplyEnum(field, schema);

        if (schema.TryGetProperty("default", out var def))
            field.DefaultValue = ReadValue(def);

        // Composition
        if (fieldType == FieldType.Object && schema.TryGetProperty("properties", out var childProps))
        {
            var childRequired = ReadRequired(schema);
            foreach (var p in childProps.EnumerateObject())
                field.Children.Add(BuildField(p.Name, p.Value, childRequired.Contains(p.Name)));
        }
        else if (fieldType == FieldType.Array && schema.TryGetProperty("items", out var items))
        {
            var itemType = ReadString(items, "type");
            var itemFieldType = MapType(itemType, ReadString(items, "format"), items);
            if (itemFieldType == FieldType.Object && items.TryGetProperty("properties", out var itemProps))
            {
                var template = new FieldDefinition("item", FieldType.Object);
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
                field.Validators.Add(new CollectionSizeRule(field.MinItems, field.MaxItems));
        }

        return field;
    }

    private static FieldType MapType(string? type, string? format, JsonElement schema)
    {
        if (schema.TryGetProperty("enum", out _)) return FieldType.Select;

        return type switch
        {
            "integer" => FieldType.Integer,
            "number" => FieldType.Number,
            "boolean" => FieldType.Checkbox,
            "object" => FieldType.Object,
            "array" => FieldType.Array,
            "string" => format switch
            {
                "email" => FieldType.Email,
                "password" => FieldType.Password,
                "uri" or "url" => FieldType.Url,
                "date" => FieldType.Date,
                "date-time" => FieldType.DateTime,
                "time" => FieldType.Time,
                "color" => FieldType.Color,
                _ => FieldType.Text
            },
            _ => FieldType.Text
        };
    }

    private static void ApplyWidget(FieldDefinition field, string? widget)
    {
        if (string.IsNullOrEmpty(widget)) return;
        field.Type = widget switch
        {
            "textarea" => FieldType.TextArea,
            "radio" => FieldType.Radio,
            "multiselect" => FieldType.MultiSelect,
            "range" => FieldType.Range,
            "color" => FieldType.Color,
            "tel" => FieldType.Tel,
            "password" => FieldType.Password,
            "select" => FieldType.Select,
            _ => field.Type
        };
    }

    private static void ApplyConstraints(FieldDefinition field, JsonElement schema)
    {
        if (schema.TryGetProperty("minLength", out var minL) && minL.TryGetInt32(out var minLen))
        {
            field.MinLength = minLen;
            field.Validators.Add(new MinLengthRule(minLen));
        }
        if (schema.TryGetProperty("maxLength", out var maxL) && maxL.TryGetInt32(out var maxLen))
        {
            field.MaxLength = maxLen;
            field.Validators.Add(new MaxLengthRule(maxLen));
        }
        double? min = null, max = null;
        if (schema.TryGetProperty("minimum", out var mn) && mn.TryGetDouble(out var mnv)) min = mnv;
        if (schema.TryGetProperty("maximum", out var mx) && mx.TryGetDouble(out var mxv)) max = mxv;
        if (min is not null || max is not null)
        {
            field.Min = min;
            field.Max = max;
            field.Validators.Add(new RangeRule(min, max));
        }
        if (schema.TryGetProperty("pattern", out var pat) && pat.GetString() is { } pattern)
        {
            field.Pattern = pattern;
            field.Validators.Add(new PatternRule(pattern));
        }
    }

    private static void ApplyEnum(FieldDefinition field, JsonElement schema)
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
            field.Options.Add(new SelectOption(value, label));
            i++;
        }

        if (field.Type is not (FieldType.Select or FieldType.MultiSelect or FieldType.Radio))
            field.Type = FieldType.Select;
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
