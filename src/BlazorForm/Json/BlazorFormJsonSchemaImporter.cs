using System.Text.Json;

namespace BlazorForm;

/// <summary>
/// Builds a <see cref="BlazorFormDefinition"/> from a JSON Schema document (draft-07 through 2020-12).
/// </summary>
/// <remarks>
/// <para>
/// Supported keywords: <c>type</c> (including nullable unions such as <c>["string","null"]</c>),
/// <c>properties</c>, <c>required</c>, <c>enum</c>, <c>const</c>, <c>format</c>, <c>default</c>,
/// <c>title</c>, <c>description</c>, <c>readOnly</c>, <c>minimum</c>/<c>maximum</c> and their
/// exclusive forms, <c>multipleOf</c>, <c>minLength</c>/<c>maxLength</c>, <c>pattern</c>,
/// <c>items</c>, <c>minItems</c>/<c>maxItems</c>, <c>uniqueItems</c>, <c>$ref</c> into
/// <c>$defs</c>/<c>definitions</c>, <c>allOf</c> (merged), <c>if</c>/<c>then</c>/<c>else</c> and
/// <c>dependentRequired</c>/<c>dependencies</c> (both mapped onto conditional requiredness).
/// </para>
/// <para>
/// UI intent that JSON Schema cannot express travels in <c>x-</c> extensions: <c>x-widget</c>,
/// <c>x-order</c>, <c>x-placeholder</c>, <c>x-autocomplete</c>, <c>x-columns</c>, <c>x-colSpan</c>,
/// <c>x-accept</c>, <c>x-multiple</c>, <c>x-clearOnHide</c>, <c>x-visibleWhen</c>,
/// <c>x-disabledWhen</c>, <c>x-readOnlyWhen</c>, <c>x-requiredWhen</c>, <c>x-steps</c> and <c>enumNames</c>.
/// </para>
/// </remarks>
public static class BlazorFormJsonSchemaImporter
{
    /// <summary>Guards against a schema whose <c>$ref</c>s form a cycle.</summary>
    private const int MaxRefDepth = 32;

    public static BlazorFormDefinition Import(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return Import(doc.RootElement);
    }

    /// <summary>
    /// Parses a schema, returning false instead of throwing when the document is not valid JSON — the
    /// shape you want behind a "paste your schema here" editor.
    /// </summary>
    public static bool TryImport(string json, out BlazorFormDefinition definition, out string? error)
    {
        try
        {
            definition = Import(json);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
        {
            definition = new BlazorFormDefinition();
            error = ex.Message;
            return false;
        }
    }

    public static BlazorFormDefinition Import(JsonElement root)
    {
        var ctx = new ImportContext(root);
        var resolved = ctx.Resolve(root);

        var form = new BlazorFormDefinition();
        if (resolved.TryGetProperty("title", out var title)) form.Title = title.GetString();
        if (resolved.TryGetProperty("description", out var desc)) form.Description = desc.GetString();
        if (resolved.TryGetProperty("x-columns", out var columns) && columns.TryGetInt32(out var cols))
            form.Columns = Math.Max(1, cols);

        // allOf is the standard "extend this schema" idiom; merging it first means the branches'
        // properties and required lists are visible to everything below.
        var merged = ctx.MergeAllOf(resolved);
        var required = ReadRequired(merged);

        if (merged.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in props.EnumerateObject())
                form.Fields.Add(BuildField(ctx, prop.Name, prop.Value, required.Contains(prop.Name)));
        }

        ApplyConditionalRequiredness(ctx, merged, form.Fields);
        ReadSteps(merged, form);

        // Order fields by x-order when present.
        form.Fields = form.Fields.OrderBy(f => f.Order).ToList();
        return form;
    }

    // ---------------------------------------------------------------- fields

    private static BlazorFormFieldDefinition BuildField(ImportContext ctx, string name, JsonElement rawSchema, bool required)
    {
        var schema = ctx.MergeAllOf(ctx.CollapseUnion(ctx.Resolve(rawSchema)));

        var type = ReadTypeName(schema);
        var format = ReadString(schema, "format");
        var widget = ReadString(schema, "x-widget");

        var fieldType = MapType(type, format, schema);
        var field = new BlazorFormFieldDefinition(name, fieldType)
        {
            Label = ReadString(schema, "title") ?? BlazorFormFieldBuilder.Humanize(name),
            HelpText = ReadString(schema, "description"),
            Placeholder = ReadString(schema, "x-placeholder"),
            Group = ReadString(schema, "x-group"),
            Autocomplete = ReadString(schema, "x-autocomplete"),
            InputMode = ReadString(schema, "x-inputMode"),
            Prefix = ReadString(schema, "x-prefix"),
            Suffix = ReadString(schema, "x-suffix"),
            Accept = ReadString(schema, "x-accept"),
            Required = required
        };

        if (schema.TryGetProperty("x-order", out var order) && order.TryGetInt32(out var o))
            field.Order = o;
        if (schema.TryGetProperty("x-colSpan", out var span) && span.TryGetInt32(out var s))
            field.ColumnSpan = s;
        if (schema.TryGetProperty("readOnly", out var ro) && ro.ValueKind == JsonValueKind.True)
            field.ReadOnly = true;
        if (schema.TryGetProperty("x-multiple", out var multiple) && multiple.ValueKind == JsonValueKind.True)
            field.Multiple = true;
        if (schema.TryGetProperty("x-clearOnHide", out var clear) && clear.ValueKind == JsonValueKind.True)
            field.ClearOnHide = true;
        if (schema.TryGetProperty("x-autofocus", out var autofocus) && autofocus.ValueKind == JsonValueKind.True)
            field.Autofocus = true;
        if (schema.TryGetProperty("x-showLabel", out var showLabel) && showLabel.ValueKind == JsonValueKind.False)
            field.ShowLabel = false;
        if (schema.TryGetProperty("x-characterCount", out var counter) && counter.ValueKind == JsonValueKind.True)
            field.ShowCharacterCount = true;
        if (ReadString(schema, "x-updateOn") == "input")
            field.UpdateOn = BlazorFormUpdateTrigger.Input;
        if (schema.TryGetProperty("x-debounce", out var debounce) && debounce.TryGetInt32(out var ms) && ms > 0)
            field.DebounceMilliseconds = ms;
        if (schema.TryGetProperty("x-maxFileSize", out var maxFile) && maxFile.TryGetInt64(out var bytes))
            field.MaxFileSize = bytes;
        if (schema.TryGetProperty("x-step", out var step) && step.TryGetDouble(out var stepValue))
            field.NumericStep = stepValue;
        if (schema.TryGetProperty("x-revalidateOn", out var revalidate) && revalidate.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in revalidate.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String) continue;
                if (item.GetString() is { Length: > 0 } dependency && !field.RevalidateOn.Contains(dependency))
                    field.RevalidateOn.Add(dependency);
            }
        }

        // `examples` is JSON Schema's own "values like these", which is what a datalist offers.
        if (schema.TryGetProperty("examples", out var examples) && examples.ValueKind == JsonValueKind.Array)
            foreach (var example in examples.EnumerateArray())
                if (BlazorFormConditionJson.ReadValue(example) is { } value)
                    field.Suggestions.Add(BlazorFormValueConverter.ToInvariantString(value));

        ReadAttributes(schema, "x-attributes", field.Attributes);
        ReadAttributes(schema, "x-inputAttributes", field.InputAttributes);

        if (required)
            field.AddValidator(new BlazorFormRequiredRule());

        ApplyWidget(field, schema, widget);
        ApplyConstraints(field, schema);
        ApplyEnum(field, schema);
        ApplyConditions(field, schema);

        if (schema.TryGetProperty("default", out var def))
            field.DefaultValue = BlazorFormConditionJson.ReadValue(def);

        // Composition
        if (fieldType == BlazorFormFieldType.Object && schema.TryGetProperty("properties", out var childProps))
        {
            var childRequired = ReadRequired(schema);
            foreach (var p in childProps.EnumerateObject())
                field.Children.Add(BuildField(ctx, p.Name, p.Value, childRequired.Contains(p.Name)));

            ApplyConditionalRequiredness(ctx, schema, field.Children);
            field.Children = field.Children.OrderBy(c => c.Order).ToList();
        }
        // The declared JSON type decides this, not the control the field ended up as. A multi-select is
        // `"type": "array"` carrying its choices as a top-level `enum`, which reads as a Select before
        // the widget hint corrects it — and testing the mapped type meant `minItems`, `maxItems` and
        // `uniqueItems` were skipped for exactly the fields most likely to declare them. "Choose at
        // least one" survived the export and was dropped on the way back in.
        else if (IsArraySchema(type, fieldType, field.Type))
        {
            BuildArrayItems(ctx, field, schema, widget);
        }

        return field;
    }

    /// <summary>
    /// Whether this schema describes an array's contents, whatever control it renders as. A field is
    /// one when the document says <c>"type": "array"</c>, or when the mapping arrived at a type that
    /// holds several values.
    /// </summary>
    private static bool IsArraySchema(string? declaredType, BlazorFormFieldType mapped, BlazorFormFieldType actual)
        => string.Equals(declaredType, "array", StringComparison.Ordinal)
           || mapped == BlazorFormFieldType.Array
           || actual is BlazorFormFieldType.Array or BlazorFormFieldType.MultiSelect or BlazorFormFieldType.Tags;

    private static void BuildArrayItems(
        ImportContext ctx, BlazorFormFieldDefinition field, JsonElement schema, string? widget)
    {
        // A field the widget hint has already settled — an exported multi-select or tag list — has no
        // per-row template to build; only its size constraints are still to read, at the bottom.
        var buildTemplate = field.Type is BlazorFormFieldType.Array;

        // 2020-12 renamed the tuple form to prefixItems; the single-schema `items` is what forms use.
        if (buildTemplate && schema.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Object)
        {
            var resolvedItems = ctx.MergeAllOf(ctx.CollapseUnion(ctx.Resolve(items)));
            var itemFieldType = MapType(ReadTypeName(resolvedItems), ReadString(resolvedItems, "format"), resolvedItems);

            if (itemFieldType == BlazorFormFieldType.Object && resolvedItems.TryGetProperty("properties", out var itemProps))
            {
                var template = new BlazorFormFieldDefinition("item", BlazorFormFieldType.Object);
                var itemRequired = ReadRequired(resolvedItems);
                foreach (var p in itemProps.EnumerateObject())
                    template.Children.Add(BuildField(ctx, p.Name, p.Value, itemRequired.Contains(p.Name)));
                template.Children = template.Children.OrderBy(c => c.Order).ToList();
                field.ItemTemplate = template;
            }
            else
            {
                field.ItemTemplate = BuildField(ctx, "item", items, required: false);
            }
        }

        var uniqueItems = schema.TryGetProperty("uniqueItems", out var unique) && unique.ValueKind == JsonValueKind.True;

        // An array of a fixed set of choices, each usable once, is a set of tick boxes rather than a
        // repeater — "add a row, open a dropdown, pick Monday; add a row, open a dropdown, pick
        // Tuesday" is the wrong shape for a question with three answers. This is the rule
        // react-jsonschema-form uses, and `uniqueItems` is what makes it safe: without it the document
        // is explicitly allowing the same value twice, which only a repeater can express.
        //
        // A document that named its own control is taken at its word, so a schema this library
        // exported — which always writes `x-widget` — comes back as the form it left as. The default
        // is only for a schema that never said.
        if (buildTemplate && uniqueItems && widget is null
            && field.Type == BlazorFormFieldType.Array
            && field.ItemTemplate is { Type: BlazorFormFieldType.Select, Options.Count: > 0 } choices)
        {
            field.Type = BlazorFormFieldType.MultiSelect;
            foreach (var option in choices.Options) field.Options.Add(option);
            field.ItemTemplate = null;
            uniqueItems = false; // a set cannot hold a duplicate, so the rule has nothing left to say
        }

        if (schema.TryGetProperty("minItems", out var mi) && mi.TryGetInt32(out var minI)) field.MinItems = minI;
        if (schema.TryGetProperty("maxItems", out var ma) && ma.TryGetInt32(out var maxI)) field.MaxItems = maxI;
        if (field.MinItems is not null || field.MaxItems is not null)
            field.AddValidator(new BlazorFormCollectionSizeRule(field.MinItems, field.MaxItems));

        if (uniqueItems) field.AddValidator(new BlazorFormUniqueItemsRule());
    }

    // ---------------------------------------------------------------- keyword mapping

    /// <summary>
    /// Reads <c>type</c>, tolerating the union form used for nullable values —
    /// <c>["string","null"]</c> means "a string, optionally absent".
    /// </summary>
    private static string? ReadTypeName(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var type)) return null;
        if (type.ValueKind == JsonValueKind.String) return type.GetString();
        if (type.ValueKind != JsonValueKind.Array) return null;

        return type.EnumerateArray()
            .Select(t => t.GetString())
            .FirstOrDefault(t => t is not null && !string.Equals(t, "null", StringComparison.Ordinal));
    }

    private static BlazorFormFieldType MapType(string? type, string? format, JsonElement schema)
    {
        if (schema.TryGetProperty("enum", out _)) return BlazorFormFieldType.Select;
        if (ConstBranches(schema) is not null) return BlazorFormFieldType.Select;

        return type switch
        {
            "integer" => BlazorFormFieldType.Integer,
            "number" => BlazorFormFieldType.Number,
            "boolean" => BlazorFormFieldType.Checkbox,
            "object" => BlazorFormFieldType.Object,
            "array" => BlazorFormFieldType.Array,
            "string" => MapStringFormat(format, schema),
            // No type at all: an object with properties is still an object.
            null when schema.TryGetProperty("properties", out _) => BlazorFormFieldType.Object,
            null when schema.TryGetProperty("items", out _) => BlazorFormFieldType.Array,
            _ => BlazorFormFieldType.Text
        };
    }

    private static BlazorFormFieldType MapStringFormat(string? format, JsonElement schema)
    {
        // A base64/binary payload is a file upload, not a text box.
        if (ReadString(schema, "contentEncoding") is "base64" || ReadString(schema, "contentMediaType") is not null)
            return BlazorFormFieldType.File;

        return format switch
        {
            "email" or "idn-email" => BlazorFormFieldType.Email,
            "password" => BlazorFormFieldType.Password,
            "uri" or "url" or "iri" => BlazorFormFieldType.Url,
            "date" => BlazorFormFieldType.Date,
            "date-time" => BlazorFormFieldType.DateTime,
            "time" => BlazorFormFieldType.Time,
            "color" => BlazorFormFieldType.Color,
            "tel" or "phone" => BlazorFormFieldType.Tel,
            "binary" => BlazorFormFieldType.File,
            _ => BlazorFormFieldType.Text
        };
    }

    private static void ApplyWidget(BlazorFormFieldDefinition field, JsonElement schema, string? widget)
    {
        // An explicit renderer key wins over everything: it names a component the app registered, and
        // no built-in type can substitute for it.
        if (ReadString(schema, "x-renderer") is { Length: > 0 } renderer)
        {
            field.CustomRenderer = renderer;
            if (widget is null or "custom") field.Type = BlazorFormFieldType.Custom;
        }

        if (string.IsNullOrEmpty(widget)) return;

        field.Type = widget switch
        {
            "array" => BlazorFormFieldType.Array,
            "textarea" => BlazorFormFieldType.TextArea,
            "radio" => BlazorFormFieldType.Radio,
            "multiselect" => BlazorFormFieldType.MultiSelect,
            "combobox" => BlazorFormFieldType.Combobox,
            "tags" => BlazorFormFieldType.Tags,
            "range" => BlazorFormFieldType.Range,
            "color" => BlazorFormFieldType.Color,
            "tel" => BlazorFormFieldType.Tel,
            "search" => BlazorFormFieldType.Search,
            "password" => BlazorFormFieldType.Password,
            "select" => BlazorFormFieldType.Select,
            "file" => BlazorFormFieldType.File,
            "hidden" => BlazorFormFieldType.Hidden,
            "static" => BlazorFormFieldType.Static,
            "switch" => BlazorFormFieldType.Checkbox,
            _ => field.Type
        };

        // "switch" is a checkbox with a different affordance, so the intent travels as a renderer hint
        // rather than as a field type of its own.
        if (widget == "switch") field.Attributes["switch"] = true;
    }

    /// <summary>Reads a renderer-hint bag written by the exporter back onto a field.</summary>
    private static void ReadAttributes(JsonElement schema, string property, IDictionary<string, object?> into)
    {
        if (!schema.TryGetProperty(property, out var bag) || bag.ValueKind != JsonValueKind.Object) return;
        foreach (var entry in bag.EnumerateObject())
            into[entry.Name] = BlazorFormConditionJson.ReadValue(entry.Value);
    }

    private static void ApplyConstraints(BlazorFormFieldDefinition field, JsonElement schema)
    {
        if (schema.TryGetProperty("minLength", out var minL) && minL.TryGetInt32(out var minLen))
        {
            field.MinLength = minLen;
            field.AddValidator(new BlazorFormMinLengthRule(minLen));
        }
        if (schema.TryGetProperty("maxLength", out var maxL) && maxL.TryGetInt32(out var maxLen))
        {
            field.MaxLength = maxLen;
            field.AddValidator(new BlazorFormMaxLengthRule(maxLen));
        }

        double? min = null, max = null;
        if (schema.TryGetProperty("minimum", out var mn) && mn.TryGetDouble(out var mnv)) min = mnv;
        if (schema.TryGetProperty("maximum", out var mx) && mx.TryGetDouble(out var mxv)) max = mxv;

        // Exclusive bounds have no exact equivalent in an inclusive rule (or in the HTML min/max
        // attributes), so they are nudged by one step: whole numbers for integers, an epsilon otherwise.
        if (schema.TryGetProperty("exclusiveMinimum", out var emn) && emn.TryGetDouble(out var emnv))
            min = emnv + ExclusiveStep(field, emnv);
        if (schema.TryGetProperty("exclusiveMaximum", out var emx) && emx.TryGetDouble(out var emxv))
            max = emxv - ExclusiveStep(field, emxv);

        if (min is not null || max is not null)
        {
            field.Min = min;
            field.Max = max;
            field.AddValidator(new BlazorFormRangeRule(min, max));
        }

        if (schema.TryGetProperty("multipleOf", out var mo) && mo.TryGetDouble(out var factor) && factor > 0)
        {
            field.NumericStep ??= factor;
            field.AddValidator(new BlazorFormMultipleOfRule(factor));
        }

        if (schema.TryGetProperty("pattern", out var pat) && pat.GetString() is { } pattern)
        {
            field.Pattern = pattern;
            field.AddValidator(new BlazorFormPatternRule(pattern));
        }

        // A `format` that maps to a semantic rule gets that rule too, not just the input type.
        switch (ReadString(schema, "format"))
        {
            case "email" or "idn-email":
                field.AddValidator(new BlazorFormEmailRule());
                break;
            case "uri" or "url" or "iri":
                field.AddValidator(new BlazorFormUrlRule());
                break;
            case "uuid":
                field.Pattern ??= "^[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$";
                field.AddValidator(new BlazorFormPatternRule(field.Pattern, "Enter a valid UUID."));
                break;
        }
    }

    private static double ExclusiveStep(BlazorFormFieldDefinition field, double bound)
        => field.Type == BlazorFormFieldType.Integer ? 1 : Math.Max(Math.Abs(bound), 1) * 1e-9;

    private static void ApplyEnum(BlazorFormFieldDefinition field, JsonElement schema)
    {
        // `const` is a one-value enum: the field is fixed, so it is shown read-only with that value.
        if (schema.TryGetProperty("const", out var constant))
        {
            field.DefaultValue = BlazorFormConditionJson.ReadValue(constant);
            field.ReadOnly = true;
            return;
        }

        // `oneOf` of `const` values is the standard way to give an enum member a display label, since
        // JSON Schema's own `enum` has nowhere to put one.
        if (ConstBranches(schema) is { } branches)
        {
            foreach (var branch in branches)
            {
                var raw = branch.GetProperty("const");
                var value = raw.ValueKind == JsonValueKind.String ? raw.GetString()! : raw.ToString();
                field.Options.Add(new BlazorFormSelectOption(value, ReadString(branch, "title") ?? value));
            }
            if (!field.IsChoice) field.Type = BlazorFormFieldType.Select;
            return;
        }

        if (!schema.TryGetProperty("enum", out var en) || en.ValueKind != JsonValueKind.Array)
            return;

        var names = ReadStringArray(schema, "enumNames");
        var groups = ReadStringArray(schema, "x-enumGroups");

        // A set rather than a positional array: which options are disabled is a property of the values,
        // not of their order, and a schema hand-written by someone else will spell it that way.
        var disabled = new HashSet<string>(StringComparer.Ordinal);
        if (schema.TryGetProperty("x-enumDisabled", out var dis) && dis.ValueKind == JsonValueKind.Array)
            foreach (var d in dis.EnumerateArray())
                disabled.Add(d.ValueKind == JsonValueKind.String ? d.GetString()! : d.ToString());

        var i = 0;
        foreach (var item in en.EnumerateArray())
        {
            var value = item.ValueKind == JsonValueKind.String ? item.GetString()! : item.ToString();
            var label = names is not null && i < names.Length ? names[i] : value;
            var group = groups is not null && i < groups.Length && groups[i].Length > 0 ? groups[i] : null;
            field.Options.Add(new BlazorFormSelectOption(value, label, disabled.Contains(value), group));
            i++;
        }

        if (!field.IsChoice) field.Type = BlazorFormFieldType.Select;
    }

    /// <summary>
    /// The branches of an <c>anyOf</c>/<c>oneOf</c> when every one of them pins a single <c>const</c> —
    /// the shape that means "an enum, with a label per member". Null for anything else, including a
    /// union of object shapes, which describes a choice of subforms rather than a choice of values.
    /// </summary>
    private static IReadOnlyList<JsonElement>? ConstBranches(JsonElement schema)
    {
        var keyword = schema.TryGetProperty("oneOf", out var one) && one.ValueKind == JsonValueKind.Array ? one
            : schema.TryGetProperty("anyOf", out var any) && any.ValueKind == JsonValueKind.Array ? any
            : default;
        if (keyword.ValueKind != JsonValueKind.Array) return null;

        var branches = keyword.EnumerateArray().ToList();
        return branches.Count > 0
               && branches.All(b => b.ValueKind == JsonValueKind.Object && b.TryGetProperty("const", out _))
            ? branches
            : null;
    }

    private static void ApplyConditions(BlazorFormFieldDefinition field, JsonElement schema)
    {
        if (schema.TryGetProperty("x-visibleWhen", out var visible))
            field.VisibleWhen = BlazorFormConditionJson.Read(visible);
        if (schema.TryGetProperty("x-disabledWhen", out var disabled))
            field.DisabledWhen = BlazorFormConditionJson.Read(disabled);
        if (schema.TryGetProperty("x-readOnlyWhen", out var readOnly))
            field.ReadOnlyWhen = BlazorFormConditionJson.Read(readOnly);
        if (schema.TryGetProperty("x-requiredWhen", out var requiredWhen))
            field.RequiredWhen = BlazorFormConditionJson.Read(requiredWhen);
    }

    // ---------------------------------------------------------------- conditional requiredness

    /// <summary>
    /// Maps the two standard ways a schema says "this field is required only sometimes" onto
    /// <see cref="BlazorFormFieldDefinition.RequiredWhen"/>:
    /// <c>dependentRequired</c>/<c>dependencies</c> ("required once that one is filled in") and the
    /// <c>if</c>/<c>then</c> pair whose <c>if</c> pins properties to a <c>const</c> or <c>enum</c>.
    /// </summary>
    private static void ApplyConditionalRequiredness(
        ImportContext ctx, JsonElement schema, IList<BlazorFormFieldDefinition> fields)
    {
        if (schema.TryGetProperty("dependentRequired", out var dependent) && dependent.ValueKind == JsonValueKind.Object)
            ApplyDependentRequired(dependent, fields);

        // Draft-07 spelled it `dependencies`, with an array value meaning the same thing.
        if (schema.TryGetProperty("dependencies", out var dependencies) && dependencies.ValueKind == JsonValueKind.Object)
            ApplyDependentRequired(dependencies, fields);

        ApplyIfThen(ctx, schema, fields);
    }

    private static void ApplyDependentRequired(JsonElement map, IList<BlazorFormFieldDefinition> fields)
    {
        foreach (var entry in map.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.Array) continue;

            var trigger = new BlazorFormFieldCondition(entry.Name, BlazorFormConditionOperator.IsNotEmpty);
            foreach (var dependentName in entry.Value.EnumerateArray())
            {
                if (dependentName.GetString() is not { } target) continue;
                var field = fields.FirstOrDefault(f => string.Equals(f.Name, target, StringComparison.OrdinalIgnoreCase));
                if (field is null || field.Required) continue;
                field.RequiredWhen = Combine(field.RequiredWhen, trigger);
            }
        }
    }

    private static void ApplyIfThen(ImportContext ctx, JsonElement schema, IList<BlazorFormFieldDefinition> fields)
    {
        if (!schema.TryGetProperty("if", out var ifSchema)) return;

        var condition = ReadIfCondition(ctx.Resolve(ifSchema));
        if (condition is null) return;

        if (schema.TryGetProperty("then", out var thenSchema))
            ApplyBranch(ctx.Resolve(thenSchema), condition);

        if (schema.TryGetProperty("else", out var elseSchema))
        {
            // "Otherwise" is the negation, which only round-trips cleanly for a single comparison.
            var negated = Negate(condition);
            if (negated is not null) ApplyBranch(ctx.Resolve(elseSchema), negated);
        }

        void ApplyBranch(JsonElement branch, IBlazorFormCondition when)
        {
            foreach (var name in ReadRequired(branch))
            {
                var field = fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
                if (field is null || field.Required) continue;
                field.RequiredWhen = Combine(field.RequiredWhen, when);
            }
        }
    }

    /// <summary>
    /// Turns an <c>if</c> subschema into a condition, for the shape forms actually use:
    /// <c>{"properties": {"kind": {"const": "business"}}}</c>. Anything more expressive is beyond what
    /// a UI condition can represent, and is ignored rather than half-applied.
    /// </summary>
    private static IBlazorFormCondition? ReadIfCondition(JsonElement ifSchema)
    {
        if (!ifSchema.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object)
            return null;

        var parts = new List<IBlazorFormCondition>();
        foreach (var prop in props.EnumerateObject())
        {
            if (prop.Value.TryGetProperty("const", out var constant))
            {
                parts.Add(new BlazorFormFieldCondition(prop.Name, BlazorFormConditionOperator.Equals,
                    BlazorFormConditionJson.ReadValue(constant)));
            }
            else if (prop.Value.TryGetProperty("enum", out var en) && en.ValueKind == JsonValueKind.Array)
            {
                parts.Add(new BlazorFormFieldCondition(prop.Name, BlazorFormConditionOperator.In,
                    BlazorFormConditionJson.ReadValue(en)));
            }
        }

        return parts.Count switch
        {
            0 => null,
            1 => parts[0],
            _ => new BlazorFormConditionGroup(BlazorFormConditionLogic.And, [.. parts])
        };
    }

    private static IBlazorFormCondition? Negate(IBlazorFormCondition condition) => condition switch
    {
        BlazorFormFieldCondition { Operator: BlazorFormConditionOperator.Equals } c
            => new BlazorFormFieldCondition(c.FieldPath, BlazorFormConditionOperator.NotEquals, c.Value),
        BlazorFormFieldCondition { Operator: BlazorFormConditionOperator.In } c
            => new BlazorFormFieldCondition(c.FieldPath, BlazorFormConditionOperator.NotIn, c.Value),
        _ => null
    };

    private static IBlazorFormCondition Combine(IBlazorFormCondition? existing, IBlazorFormCondition addition)
        => existing is null ? addition : BlazorFormConditionGroup.Any(existing, addition);

    // ---------------------------------------------------------------- steps

    private static void ReadSteps(JsonElement schema, BlazorFormDefinition form)
    {
        if (!schema.TryGetProperty("x-steps", out var steps) || steps.ValueKind != JsonValueKind.Array)
            return;

        foreach (var element in steps.EnumerateArray())
        {
            var id = ReadString(element, "id");
            if (id is null) continue;

            var step = new BlazorFormStep(id, ReadString(element, "title"))
            {
                Description = ReadString(element, "description")
            };

            if (element.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Array)
                foreach (var f in fields.EnumerateArray())
                    if (f.GetString() is { } name) step.Fields.Add(name);

            if (element.TryGetProperty("visibleWhen", out var visible))
                step.VisibleWhen = BlazorFormConditionJson.Read(visible);

            form.Steps.Add(step);
        }
    }

    // ---------------------------------------------------------------- helpers

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

    private static string[]? ReadStringArray(JsonElement element, string property)
        => element.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Select(e => e.GetString() ?? "").ToArray()
            : null;

    /// <summary>
    /// Resolves <c>$ref</c> pointers and flattens <c>allOf</c> against the document they came from.
    /// </summary>
    private sealed class ImportContext(JsonElement root)
    {
        private readonly JsonElement _root = root;

        /// <summary>Follows a chain of local <c>$ref</c>s to the schema they ultimately name.</summary>
        public JsonElement Resolve(JsonElement schema)
        {
            var current = schema;
            for (var depth = 0; depth < MaxRefDepth; depth++)
            {
                if (current.ValueKind != JsonValueKind.Object) return current;
                if (!current.TryGetProperty("$ref", out var refElement) || refElement.ValueKind != JsonValueKind.String)
                    return current;

                var pointer = refElement.GetString();
                if (pointer is null || !TryResolvePointer(pointer, out var target)) return current;
                current = target;
            }
            // A reference cycle: stop where we are rather than recursing forever.
            return current;
        }

        /// <summary>
        /// Folds every <c>allOf</c> branch into one schema object. Branches are applied in order and
        /// the outer schema wins, which matches how <c>allOf</c> is used in practice ("this, plus the
        /// shared base"). <c>required</c> and <c>properties</c> are unioned rather than replaced.
        /// </summary>
        public JsonElement MergeAllOf(JsonElement schema)
        {
            if (schema.ValueKind != JsonValueKind.Object) return schema;
            if (!schema.TryGetProperty("allOf", out var allOf) || allOf.ValueKind != JsonValueKind.Array)
                return schema;

            return Merge(allOf.EnumerateArray().Select(Resolve).Select(MergeAllOf).Append(schema));
        }

        /// <summary>
        /// Collapses the one <c>anyOf</c>/<c>oneOf</c> shape that has an unambiguous single-field
        /// meaning: a union whose only other branch is <c>null</c>. That is how most generators spell
        /// "optional string", and it describes exactly one control.
        /// </summary>
        /// <remarks>
        /// A union of genuinely different object shapes is a different question — it needs a selector
        /// and a subform that swaps with it — and guessing a branch would silently bind the user's
        /// answers to the wrong one. Those are left alone rather than half-applied, on the same
        /// principle as a condition that has no JSON form.
        /// </remarks>
        public JsonElement CollapseUnion(JsonElement schema)
        {
            if (schema.ValueKind != JsonValueKind.Object) return schema;

            var keyword = schema.TryGetProperty("anyOf", out var any) && any.ValueKind == JsonValueKind.Array ? any
                : schema.TryGetProperty("oneOf", out var one) && one.ValueKind == JsonValueKind.Array ? one
                : default;
            if (keyword.ValueKind != JsonValueKind.Array) return schema;

            // A branch list that is entirely `const` values is an enum with labels; ApplyEnum reads it
            // where it stands, so the schema is handed back untouched.
            var branches = keyword.EnumerateArray().Select(Resolve).ToList();
            if (branches.Count > 0 && branches.All(b => b.ValueKind == JsonValueKind.Object && b.TryGetProperty("const", out _)))
                return schema;

            var meaningful = branches.Where(b => ReadTypeName(b) != "null" || b.TryGetProperty("properties", out _)).ToList();
            if (meaningful.Count != 1) return schema;

            // The outer schema wins: its title and description describe the field, not the branch.
            return Merge([meaningful[0], StripUnionKeywords(schema)]);
        }

        /// <summary>Copies a schema without its union keywords, so merging it back cannot re-trigger the collapse.</summary>
        private static JsonElement StripUnionKeywords(JsonElement schema)
        {
            var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                foreach (var member in schema.EnumerateObject())
                {
                    if (member.Name is "anyOf" or "oneOf") continue;
                    writer.WritePropertyName(member.Name);
                    member.Value.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            using var doc = JsonDocument.Parse(buffer.ToArray());
            return doc.RootElement.Clone();
        }

        /// <summary>
        /// Folds a sequence of schema objects into one, later branches winning. <c>required</c> and
        /// <c>properties</c> are unioned rather than replaced.
        /// </summary>
        private static JsonElement Merge(IEnumerable<JsonElement> branches)
        {
            var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            var required = new List<string>();
            var scalars = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

            foreach (var branch in branches)
            {
                if (branch.ValueKind != JsonValueKind.Object) continue;
                foreach (var member in branch.EnumerateObject())
                {
                    switch (member.Name)
                    {
                        case "allOf":
                            break;
                        case "properties" when member.Value.ValueKind == JsonValueKind.Object:
                            foreach (var p in member.Value.EnumerateObject()) properties[p.Name] = p.Value;
                            break;
                        case "required" when member.Value.ValueKind == JsonValueKind.Array:
                            foreach (var r in member.Value.EnumerateArray())
                                if (r.GetString() is { } s && !required.Contains(s)) required.Add(s);
                            break;
                        default:
                            scalars[member.Name] = member.Value;
                            break;
                    }
                }
            }

            // The merged result is materialised as a new document so the rest of the importer can keep
            // working against plain JsonElement.
            var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                foreach (var (name, value) in scalars)
                {
                    writer.WritePropertyName(name);
                    value.WriteTo(writer);
                }
                if (properties.Count > 0)
                {
                    writer.WritePropertyName("properties");
                    writer.WriteStartObject();
                    foreach (var (name, value) in properties)
                    {
                        writer.WritePropertyName(name);
                        value.WriteTo(writer);
                    }
                    writer.WriteEndObject();
                }
                if (required.Count > 0)
                {
                    writer.WritePropertyName("required");
                    writer.WriteStartArray();
                    foreach (var r in required) writer.WriteStringValue(r);
                    writer.WriteEndArray();
                }
                writer.WriteEndObject();
            }

            // The document is kept alive by cloning the element out of it.
            using var doc = JsonDocument.Parse(buffer.ToArray());
            return doc.RootElement.Clone();
        }

        /// <summary>Resolves a local JSON pointer such as <c>#/$defs/Address</c>.</summary>
        private bool TryResolvePointer(string pointer, out JsonElement target)
        {
            target = default;
            if (!pointer.StartsWith('#')) return false; // remote references are out of scope

            var current = _root;
            foreach (var rawSegment in pointer.TrimStart('#').Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                var segment = rawSegment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
                if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(segment, out var next))
                {
                    current = next;
                }
                else if (current.ValueKind == JsonValueKind.Array
                         && int.TryParse(segment, System.Globalization.NumberStyles.Integer,
                             System.Globalization.CultureInfo.InvariantCulture, out var index)
                         && index >= 0 && index < current.GetArrayLength())
                {
                    current = current[index];
                }
                else
                {
                    return false;
                }
            }

            target = current;
            return true;
        }
    }
}
