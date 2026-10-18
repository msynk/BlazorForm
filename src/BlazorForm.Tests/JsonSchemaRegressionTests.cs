using System.Text;
using System.Text.Json;

namespace BlazorForm.Tests;

/// <summary>
/// Schemas the importer used to crash on, misread, or send back out changed. Each test names the
/// shape that went wrong and pins what it should do instead.
/// </summary>
public class JsonSchemaRegressionTests
{
    private static BlazorFormDefinition Import(string json) => BlazorFormJsonSchemaImporter.Import(json);

    private static BlazorFormDefinition RoundTrip(BlazorFormDefinition form)
        => Import(BlazorFormJsonSchemaExporter.Export(form));

    private static JsonElement Exported(BlazorFormDefinition form, string field)
    {
        using var doc = JsonDocument.Parse(BlazorFormJsonSchemaExporter.Export(form));
        return doc.RootElement.GetProperty("properties").GetProperty(field).Clone();
    }

    private static async Task<IReadOnlyList<BlazorFormValidationMessage>> Validate(
        BlazorFormDefinition form, params (string Path, object? Value)[] values)
    {
        var data = new BlazorFormDictionaryDataAccessor();
        foreach (var (path, value) in values) data.SetValue(path, value);
        return await new BlazorFormValidator().ValidateAsync(form, data);
    }

    // ---------------------------------------------------------------- recursion and budgets

    [Fact]
    public void A_recursive_reference_is_followed_one_level_and_then_cut_off()
    {
        // A tree: following `child` again expanded the definition forever and overflowed the stack,
        // which no catch can stop — TryImport took the whole process down with it. The recursion is
        // followed once (a node and its child) and the property that would repeat it again is left out.
        const string json = """
        {
          "$defs": { "Node": { "type": "object", "properties": {
            "name": { "type": "string" },
            "child": { "$ref": "#/$defs/Node" } } } },
          "$ref": "#/$defs/Node"
        }
        """;

        Assert.True(BlazorFormJsonSchemaImporter.TryImport(json, out var form, out _));
        Assert.NotNull(form.FindField("name"));
        var child = form.FindField("child")!;
        Assert.Equal(["name"], child.Children.Select(c => c.Name));
    }

    [Fact]
    public void An_optional_recursive_reference_terminates_too()
    {
        // pydantic's spelling of `child: Optional[Node]`: the reference sits inside a null union.
        const string json = """
        {
          "$defs": { "Node": { "type": "object", "properties": {
            "name": { "type": "string" },
            "child": { "anyOf": [ { "$ref": "#/$defs/Node" }, { "type": "null" } ] },
            "described": { "allOf": [ { "$ref": "#/$defs/Node" } ], "description": "pydantic v1" } } } },
          "type": "object",
          "properties": { "root": { "$ref": "#/$defs/Node" } }
        }
        """;

        var root = Import(json).FindField("root")!;

        Assert.Equal(["name", "child", "described"], root.Children.Select(c => c.Name));
        Assert.Equal(["name"], root.Children.Single(c => c.Name == "child").Children.Select(c => c.Name));
        Assert.Equal(["name"], root.Children.Single(c => c.Name == "described").Children.Select(c => c.Name));
    }

    [Fact]
    public void An_allOf_that_includes_itself_terminates()
    {
        const string json = """
        {
          "$defs": { "A": { "allOf": [ { "$ref": "#/$defs/A" } ], "properties": { "x": { "type": "string" } } } },
          "$ref": "#/$defs/A"
        }
        """;

        Assert.True(BlazorFormJsonSchemaImporter.TryImport(json, out var form, out _));
        Assert.NotNull(form.FindField("x"));
    }

    [Fact]
    public void A_recursive_array_item_falls_back_to_a_text_row()
    {
        const string json = """
        { "type": "object", "properties": { "list": { "type": "array", "items": { "$ref": "#" } } } }
        """;

        var list = Import(json).FindField("list")!;

        // One level of the document per row, and the rows of those rows are text boxes.
        Assert.Equal(BlazorFormFieldType.Array, list.Type);
        var nested = list.ItemTemplate!.Children.Single(c => c.Name == "list");
        Assert.Equal(BlazorFormFieldType.Text, nested.ItemTemplate!.Type);
    }

    [Fact]
    public void A_definition_that_extends_another_and_also_holds_one_is_not_mistaken_for_recursion()
    {
        // An employee is a person and has a manager, who is a person. Nothing here repeats forever.
        const string json = """
        {
          "$defs": {
            "Person": { "type": "object", "properties": { "name": { "type": "string" } } },
            "Employee": { "allOf": [ { "$ref": "#/$defs/Person" } ],
                          "properties": { "manager": { "$ref": "#/$defs/Person" } } }
          },
          "$ref": "#/$defs/Employee"
        }
        """;

        var form = Import(json);

        Assert.NotNull(form.FindField("name"));
        Assert.Equal(["name"], form.FindField("manager")!.Children.Select(c => c.Name));
    }

    [Fact]
    public void A_schema_that_fans_out_to_millions_of_fields_is_refused_rather_than_built()
    {
        // Each level refers to the next twice: 1.5 KB of JSON describing 2^16 fields.
        var defs = new StringBuilder();
        for (var i = 0; i < 16; i++)
            defs.Append($"\"L{i}\":{{\"type\":\"object\",\"properties\":{{\"a\":{{\"$ref\":\"#/$defs/L{i + 1}\"}},\"b\":{{\"$ref\":\"#/$defs/L{i + 1}\"}}}}}},");
        defs.Append("\"L16\":{\"type\":\"string\"}");
        var json = "{\"$defs\":{" + defs + "},\"$ref\":\"#/$defs/L0\"}";

        Assert.False(BlazorFormJsonSchemaImporter.TryImport(json, out _, out var error));
        Assert.Contains("fields", error, StringComparison.Ordinal);
        Assert.Throws<FormatException>(() => Import(json));
    }

    [Fact]
    public void A_schema_nested_beyond_any_real_form_is_refused()
    {
        var defs = new StringBuilder();
        for (var i = 0; i < 40; i++)
            defs.Append($"\"L{i}\":{{\"type\":\"object\",\"properties\":{{\"next\":{{\"$ref\":\"#/$defs/L{i + 1}\"}}}}}},");
        defs.Append("\"L40\":{\"type\":\"string\"}");
        var json = "{\"$defs\":{" + defs + "},\"$ref\":\"#/$defs/L0\"}";

        Assert.False(BlazorFormJsonSchemaImporter.TryImport(json, out _, out var error));
        Assert.Contains("levels deep", error, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- values of the wrong kind

    [Fact]
    public async Task The_draft04_boolean_exclusive_bound_makes_the_inclusive_bound_exclusive()
    {
        // OpenAPI 3.0 still speaks draft-04; reading a number out of `true` failed the whole import.
        const string json = """
        { "type": "object", "properties": { "n": { "type": "number", "minimum": 0, "exclusiveMinimum": true,
                                                   "maximum": 10, "exclusiveMaximum": false } } }
        """;

        var form = Import(json);
        var rule = Assert.Single(form.FindField("n")!.Validators.OfType<BlazorFormRangeRule>());

        Assert.True(rule.MinExclusive);
        Assert.False(rule.MaxExclusive);
        Assert.NotEmpty(await Validate(form, ("n", 0d)));
        Assert.Empty(await Validate(form, ("n", 0.5)));
        Assert.Empty(await Validate(form, ("n", 10d)));
    }

    [Fact]
    public void A_keyword_holding_the_wrong_kind_of_value_is_ignored_rather_than_failing_the_import()
    {
        const string json = """
        {
          "type": "object", "x-columns": "2", "title": 5, "required": [1, "a"],
          "properties": {
            "a": { "type": [1, "string"], "x-order": "1", "minLength": "5", "pattern": 7,
                   "enum": ["x", "y"], "enumNames": [1, "Why"], "x-step": "big" }
          },
          "x-steps": [ "not a step", { "id": "s", "fields": ["a", 2] } ],
          "dependentRequired": { "a": [3] }
        }
        """;

        Assert.True(BlazorFormJsonSchemaImporter.TryImport(json, out var form, out var error), error);
        var a = form.FindField("a")!;
        Assert.True(a.Required);
        Assert.Null(a.MinLength);
        Assert.Equal(["1", "Why"], a.Options.Select(o => o.Label));
        Assert.Equal(["a"], Assert.Single(form.Steps).Fields);
        Assert.Null(form.Title);
        Assert.Equal(1, form.Columns);
    }

    [Fact]
    public void Boolean_subschemas_mean_anything_and_nothing()
    {
        const string json = """
        { "type": "object", "properties": { "anything": true, "nothing": false,
                                            "list": { "type": "array", "items": true } } }
        """;

        var form = Import(json);

        Assert.Equal(BlazorFormFieldType.Text, form.FindField("anything")!.Type);
        Assert.Null(form.FindField("nothing"));
        Assert.Equal(BlazorFormFieldType.Text, form.FindField("list")!.ItemTemplate!.Type);
    }

    [Fact]
    public void A_document_that_is_not_a_schema_is_reported_and_a_boolean_one_is_empty()
    {
        Assert.False(BlazorFormJsonSchemaImporter.TryImport("[]", out _, out var error));
        Assert.NotNull(error);
        Assert.Throws<FormatException>(() => Import("42"));
        Assert.Empty(Import("true").Fields);
    }

    // ---------------------------------------------------------------- allOf

    [Fact]
    public async Task Every_conditional_in_an_allOf_applies()
    {
        // The standard way to write more than one if/then. Merging kept only the last.
        const string json = """
        {
          "type": "object",
          "properties": { "kind": { "type": "string" }, "vat": { "type": "string" }, "ssn": { "type": "string" } },
          "allOf": [
            { "if": { "properties": { "kind": { "const": "business" } } }, "then": { "required": ["vat"] } },
            { "if": { "properties": { "kind": { "const": "person" } } }, "then": { "required": ["ssn"] } }
          ]
        }
        """;

        var form = Import(json);

        var business = await Validate(form, ("kind", "business"));
        Assert.Contains(business, m => m.FieldPath == "vat");
        Assert.DoesNotContain(business, m => m.FieldPath == "ssn");

        var person = await Validate(form, ("kind", "person"));
        Assert.Contains(person, m => m.FieldPath == "ssn");
        Assert.DoesNotContain(person, m => m.FieldPath == "vat");
    }

    [Fact]
    public void A_property_two_branches_describe_keeps_both_descriptions()
    {
        const string json = """
        {
          "type": "object",
          "allOf": [
            { "properties": { "name": { "type": "string", "maxLength": 5 } } },
            { "properties": { "name": { "title": "Full name" } } }
          ]
        }
        """;

        var name = Import(json).FindField("name")!;

        Assert.Equal(5, name.MaxLength);
        Assert.Equal("Full name", name.Label);
    }

    [Fact]
    public void The_stricter_bound_wins_a_merge()
    {
        const string json = """
        { "type": "object", "properties": { "n": { "type": "integer", "allOf": [ { "minimum": 5, "maximum": 50 } ],
                                                   "minimum": 0, "maximum": 100 } } }
        """;

        var n = Import(json).FindField("n")!;

        Assert.Equal(5, n.Min);
        Assert.Equal(50, n.Max);
    }

    [Fact]
    public async Task DependentRequired_from_two_branches_is_unioned()
    {
        const string json = """
        {
          "type": "object",
          "properties": { "card": { "type": "string" }, "expiry": { "type": "string" }, "cvc": { "type": "string" } },
          "allOf": [ { "dependentRequired": { "card": ["expiry"] } }, { "dependentRequired": { "card": ["cvc"] } } ]
        }
        """;

        var messages = await Validate(Import(json), ("card", "4111"));

        Assert.Contains(messages, m => m.FieldPath == "expiry");
        Assert.Contains(messages, m => m.FieldPath == "cvc");
    }

    // ---------------------------------------------------------------- multi-select and tags

    [Fact]
    public void A_multi_select_exports_its_choices_on_items_where_validators_look_for_them()
    {
        // `enum` beside `"type": "array"` describes the array itself: no list of answers satisfied it.
        var form = BlazorFormBuilder.Create()
            .MultiSelect("days", f => f.Options(("mon", "Monday"), ("tue", "Tuesday")))
            .Build();

        var days = Exported(form, "days");

        Assert.False(days.TryGetProperty("enum", out _));
        Assert.True(days.GetProperty("uniqueItems").GetBoolean());
        Assert.Equal(["mon", "tue"], days.GetProperty("items").GetProperty("enum").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(["Monday", "Tuesday"], days.GetProperty("items").GetProperty("enumNames").EnumerateArray().Select(e => e.GetString()));

        var back = RoundTrip(form).FindField("days")!;
        Assert.Equal(BlazorFormFieldType.MultiSelect, back.Type);
        Assert.Equal(["Monday", "Tuesday"], back.Options.Select(o => o.Label));
        // A set cannot hold an answer twice, so it carries no rule saying so.
        Assert.DoesNotContain(back.Validators, v => v.Key == "uniqueItems");
    }

    [Fact]
    public void A_named_multi_select_reads_its_choices_from_items()
    {
        const string json = """
        { "type": "object", "properties": { "days": { "type": "array", "x-widget": "multiselect", "uniqueItems": true,
            "items": { "type": "string", "enum": ["mon", "tue"] } } } }
        """;

        var days = Import(json).FindField("days")!;

        Assert.Equal(BlazorFormFieldType.MultiSelect, days.Type);
        Assert.Equal(["mon", "tue"], days.Options.Select(o => o.Value));
    }

    [Fact]
    public void The_older_top_level_enum_multi_select_still_imports()
    {
        const string json = """
        { "type": "object", "properties": { "days": { "type": "array", "x-widget": "multiselect",
            "enum": ["mon", "tue"], "enumNames": ["Monday", "Tuesday"] } } }
        """;

        var days = Import(json).FindField("days")!;

        Assert.Equal(BlazorFormFieldType.MultiSelect, days.Type);
        Assert.Equal(["Monday", "Tuesday"], days.Options.Select(o => o.Label));
    }

    [Fact]
    public void A_tag_lists_per_tag_constraints_travel_on_items()
    {
        var form = BlazorFormBuilder.Create()
            .Tags("labels", f => f.MinLength(2).MaxLength(10).Pattern("^[a-z]+$").Suggest("bug", "idea"))
            .Build();

        var labels = Exported(form, "labels");

        Assert.False(labels.TryGetProperty("maxLength", out _));
        Assert.False(labels.TryGetProperty("pattern", out _));
        var items = labels.GetProperty("items");
        Assert.Equal(2, items.GetProperty("minLength").GetInt32());
        Assert.Equal(10, items.GetProperty("maxLength").GetInt32());
        Assert.Equal("^[a-z]+$", items.GetProperty("pattern").GetString());
        Assert.Equal(["bug", "idea"], items.GetProperty("examples").EnumerateArray().Select(e => e.GetString()));

        var back = RoundTrip(form).FindField("labels")!;
        Assert.Equal(BlazorFormFieldType.Tags, back.Type);
        Assert.Equal(2, back.MinLength);
        Assert.Equal(10, back.MaxLength);
        Assert.Equal("^[a-z]+$", back.Pattern);
        Assert.Equal(["bug", "idea"], back.Suggestions);
    }

    // ---------------------------------------------------------------- numeric constraints

    [Fact]
    public async Task A_step_and_a_multipleOf_constraint_both_survive()
    {
        var form = BlazorFormBuilder.Create()
            .Number("price", f => f.Step(0.1).MultipleOf(0.5))
            .Build();

        var price = Exported(form, "price");
        Assert.Equal(0.5, price.GetProperty("multipleOf").GetDouble());
        Assert.Equal(0.1, price.GetProperty("x-step").GetDouble());

        var back = RoundTrip(form);
        var field = back.FindField("price")!;
        Assert.Equal(0.1, field.NumericStep);
        Assert.Equal(0.5, Assert.Single(field.Validators.OfType<BlazorFormMultipleOfRule>()).Factor);
        Assert.NotEmpty(await Validate(back, ("price", 0.3)));
    }

    [Fact]
    public async Task An_inclusive_bound_stricter_than_the_exclusive_one_wins()
    {
        var form = Import("""
        { "type": "object", "properties": { "n": { "type": "integer", "minimum": 10, "exclusiveMinimum": 0 } } }
        """);

        Assert.NotEmpty(await Validate(form, ("n", 5L)));
        Assert.Empty(await Validate(form, ("n", 10L)));
    }

    [Fact]
    public async Task A_fractional_exclusive_bound_on_an_integer_accepts_the_next_whole_number()
    {
        var form = Import("""
        { "type": "object", "properties": { "n": { "type": "integer", "exclusiveMinimum": 0.5 } } }
        """);

        Assert.Empty(await Validate(form, ("n", 1L)));
        Assert.NotEmpty(await Validate(form, ("n", 0L)));
    }

    [Fact]
    public async Task Exclusive_bounds_stay_exclusive_and_keep_their_own_number()
    {
        var form = Import("""
        { "type": "object", "properties": { "n": { "type": "number", "exclusiveMaximum": 100 } } }
        """);

        var n = form.FindField("n")!;
        Assert.Equal(100, n.Max);
        Assert.Empty(await Validate(form, ("n", 99.99999999)));
        Assert.NotEmpty(await Validate(form, ("n", 100d)));

        var exported = Exported(form, "n");
        Assert.Equal(100, exported.GetProperty("exclusiveMaximum").GetDouble());
        Assert.False(exported.TryGetProperty("maximum", out _));
        Assert.True(Assert.Single(RoundTrip(form).FindField("n")!.Validators.OfType<BlazorFormRangeRule>()).MaxExclusive);
    }

    [Fact]
    public void Non_finite_numbers_do_not_fail_the_export()
    {
        // Range(0, +∞) means "no upper bound"; writing the infinity threw ArgumentException.
        var form = BlazorFormBuilder.Create()
            .Number("n", f => f.Range(0, double.PositiveInfinity).Default(double.NaN).Attr("weight", double.NaN))
            .Build();

        var n = Exported(form, "n");

        Assert.Equal(0, n.GetProperty("minimum").GetDouble());
        Assert.False(n.TryGetProperty("maximum", out _));
        Assert.False(n.TryGetProperty("default", out _));
        Assert.False(n.TryGetProperty("x-attributes", out _));
    }

    // ---------------------------------------------------------------- $ref

    [Fact]
    public void Keywords_beside_a_ref_are_kept_and_win_over_the_definitions()
    {
        const string json = """
        {
          "$defs": { "Address": { "type": "object", "title": "Address", "properties": { "city": { "type": "string" } } } },
          "type": "object",
          "properties": {
            "billing": { "$ref": "#/$defs/Address", "title": "Billing address", "description": "Where invoices go",
                         "x-order": 5, "readOnly": true }
          }
        }
        """;

        var billing = Import(json).FindField("billing")!;

        Assert.Equal("Billing address", billing.Label);
        Assert.Equal("Where invoices go", billing.HelpText);
        Assert.Equal(5, billing.Order);
        Assert.True(billing.ReadOnly);
        Assert.Equal(["city"], billing.Children.Select(c => c.Name));
    }

    [Fact]
    public void A_percent_encoded_reference_resolves()
    {
        const string json = """
        { "$defs": { "My Type": { "type": "integer" } },
          "type": "object", "properties": { "n": { "$ref": "#/$defs/My%20Type" } } }
        """;

        Assert.Equal(BlazorFormFieldType.Integer, Import(json).FindField("n")!.Type);
    }

    // ---------------------------------------------------------------- arrays

    [Theory]
    [InlineData("""{ "type": "array" }""")]
    [InlineData("""{ "type": "array", "items": [ { "type": "string" }, { "type": "integer" } ] }""")]
    [InlineData("""{ "type": "array", "prefixItems": [ { "type": "string" } ], "items": false }""")]
    public void An_array_without_a_single_item_schema_still_gets_a_row_to_render(string arraySchema)
    {
        // The repeater threw "has no item template" on the first Add.
        var form = Import($$"""{ "type": "object", "properties": { "list": {{arraySchema}} } }""");

        var list = form.FindField("list")!;
        Assert.Equal(BlazorFormFieldType.Array, list.Type);
        Assert.Equal(BlazorFormFieldType.Text, list.ItemTemplate!.Type);
        Assert.DoesNotContain(form.Validate(), d => d.Message.Contains("ItemTemplate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Conditional_requiredness_inside_array_items_is_honoured()
    {
        const string json = """
        { "type": "object", "properties": { "rows": { "type": "array", "items": {
            "type": "object",
            "properties": { "a": { "type": "string" }, "b": { "type": "string" } },
            "dependentRequired": { "a": ["b"] } } } } }
        """;

        var form = Import(json);
        Assert.NotNull(form.FindField("rows")!.ItemTemplate!.Children.Single(c => c.Name == "b").RequiredWhen);

        var row = BlazorFormDictionaryDataAccessor.NewObject();
        row["a"] = "filled";
        var messages = await Validate(form, ("rows", new List<object?> { row }));

        Assert.Contains(messages, m => m.FieldPath == "rows[0].b");
    }

    // ---------------------------------------------------------------- choices

    [Fact]
    public async Task A_closed_combobox_stays_closed_across_a_round_trip()
    {
        var form = BlazorFormBuilder.Create()
            .Combobox("city", f => f.Options(("a", "A"), ("b", "B")).AsCombobox())
            .Build();

        var back = RoundTrip(form);

        Assert.Contains(back.FindField("city")!.Validators, v => v.Key == "oneOf");
        Assert.NotEmpty(await Validate(back, ("city", "somewhere else")));
    }

    [Fact]
    public async Task An_open_combobox_does_not_claim_to_be_a_closed_set()
    {
        var form = BlazorFormBuilder.Create()
            .Combobox("city", f => f.Options(("a", "A"), ("b", "B")).AsCombobox(allowCustom: true))
            .Build();

        var city = Exported(form, "city");
        Assert.False(city.TryGetProperty("enum", out _));
        Assert.True(city.GetProperty("x-allowCustom").GetBoolean());
        Assert.Equal(["a", "b"], city.GetProperty("x-enum").EnumerateArray().Select(e => e.GetString()));

        var back = RoundTrip(form);
        var field = back.FindField("city")!;
        Assert.Equal(BlazorFormFieldType.Combobox, field.Type);
        Assert.Equal(["A", "B"], field.Options.Select(o => o.Label));
        Assert.Empty(await Validate(back, ("city", "somewhere else")));
    }

    [Fact]
    public void A_select_fed_by_a_provider_comes_back_as_a_select()
    {
        var form = BlazorFormBuilder.Create()
            .Select("city", f => f.OptionsFrom(_ => ValueTask.FromResult<IReadOnlyList<BlazorFormSelectOption>>([]), "country"))
            .Build();

        Assert.Equal(BlazorFormFieldType.Select, RoundTrip(form).FindField("city")!.Type);
    }

    [Fact]
    public void An_integer_enum_keeps_its_type()
    {
        const string json = """
        { "type": "object", "properties": { "n": { "type": "integer", "enum": [1, 2, 3], "default": 2 } } }
        """;

        var form = Import(json);
        var n = form.FindField("n")!;

        Assert.Equal(BlazorFormFieldType.Select, n.Type);
        Assert.Equal(typeof(long?), n.ValueType);
        Assert.Equal(["1", "2", "3"], n.Options.Select(o => o.Value));
        // Choosing an option stores the number, and the stored number shows as that option.
        Assert.Equal(2L, BlazorFormValueConverter.FromInputString("2", n.ValueType, n.Type));
        Assert.Equal("2", BlazorFormValueConverter.ToInputString(n.DefaultValue, n.Type));

        var exported = Exported(form, "n");
        Assert.Equal("integer", exported.GetProperty("type").GetString());
        Assert.All(exported.GetProperty("enum").EnumerateArray(), e => Assert.Equal(JsonValueKind.Number, e.ValueKind));
        Assert.Equal(JsonValueKind.Number, exported.GetProperty("default").ValueKind);
        Assert.Equal(typeof(long?), RoundTrip(form).FindField("n")!.ValueType);
    }

    [Fact]
    public void Boolean_and_decimal_enum_values_match_how_the_control_renders_them()
    {
        const string json = """
        { "type": "object", "properties": {
            "b": { "enum": [true, false, null] },
            "x": { "type": "number", "enum": [1.0, 2.5] } } }
        """;

        var form = Import(json);

        var b = form.FindField("b")!;
        Assert.Equal(["true", "false"], b.Options.Select(o => o.Value));
        Assert.Equal(typeof(bool?), b.ValueType);
        Assert.Equal("true", BlazorFormValueConverter.ToInputString(true, b.Type));

        var x = form.FindField("x")!;
        Assert.Equal(["1", "2.5"], x.Options.Select(o => o.Value));
        Assert.Equal(2.5, BlazorFormValueConverter.FromInputString("2.5", x.ValueType, x.Type));
    }

    [Fact]
    public void A_typed_multi_select_keeps_its_item_type()
    {
        const string json = """
        { "type": "object", "properties": { "levels": { "type": "array", "uniqueItems": true,
            "items": { "type": "integer", "enum": [1, 2] } } } }
        """;

        var form = Import(json);
        Assert.Equal(typeof(List<long>), form.FindField("levels")!.ValueType);

        var items = Exported(form, "levels").GetProperty("items");
        Assert.Equal("integer", items.GetProperty("type").GetString());
        Assert.All(items.GetProperty("enum").EnumerateArray(), e => Assert.Equal(JsonValueKind.Number, e.ValueKind));
    }

    [Fact]
    public void A_labelled_enum_with_a_null_branch_or_behind_refs_imports_as_a_select()
    {
        const string json = """
        {
          "$defs": { "R": { "const": "r", "title": "Red" } },
          "type": "object",
          "properties": {
            "nullable": { "oneOf": [ { "const": "r", "title": "Red" }, { "const": "g", "title": "Green" }, { "type": "null" } ] },
            "single": { "anyOf": [ { "const": "r", "title": "Red" }, { "type": "null" } ] },
            "referenced": { "oneOf": [ { "$ref": "#/$defs/R" }, { "const": "g", "title": "Green" } ] }
          }
        }
        """;

        var form = Import(json);

        Assert.Equal(["Red", "Green"], form.FindField("nullable")!.Options.Select(o => o.Label));
        var single = form.FindField("single")!;
        Assert.Equal(BlazorFormFieldType.Select, single.Type);
        Assert.False(single.ReadOnly);
        Assert.Equal(["Red", "Green"], form.FindField("referenced")!.Options.Select(o => o.Label));
    }

    // ---------------------------------------------------------------- if / then

    [Fact]
    public async Task An_if_with_a_constraint_that_has_no_condition_form_is_ignored_as_a_whole()
    {
        // Keeping only the `const` made the guardian required of every adult too.
        const string json = """
        {
          "type": "object",
          "properties": { "kind": { "type": "string" }, "age": { "type": "integer" }, "guardian": { "type": "string" } },
          "if": { "properties": { "kind": { "const": "person" }, "age": { "maximum": 17 } } },
          "then": { "required": ["guardian"] }
        }
        """;

        var form = Import(json);

        Assert.Null(form.FindField("guardian")!.RequiredWhen);
        Assert.Empty(await Validate(form, ("kind", "person"), ("age", 40L)));
    }

    [Fact]
    public async Task Required_inside_an_if_reads_as_presence()
    {
        const string json = """
        {
          "type": "object",
          "properties": { "card": { "type": "string" }, "expiry": { "type": "string" } },
          "if": { "required": ["card"] },
          "then": { "required": ["expiry"] }
        }
        """;

        var form = Import(json);

        Assert.Empty(await Validate(form));
        Assert.Contains(await Validate(form, ("card", "4111")), m => m.FieldPath == "expiry");
    }

    // ---------------------------------------------------------------- written values

    [Fact]
    public void Dates_and_times_are_written_as_iso_8601()
    {
        var form = BlazorFormBuilder.Create()
            .Date("start", f => f.Default(new DateTime(2026, 10, 18)))
            .Field("at", BlazorFormFieldType.DateTime, f => f.Default(new DateTime(2026, 10, 18, 9, 30, 0)))
            .Field("when", BlazorFormFieldType.Time, f => f.Default(new TimeOnly(14, 30)))
            .Date("day", f => f.Default(new DateOnly(2026, 1, 2)))
            .Text("note", f => f.VisibleWhen("start", BlazorFormConditionOperator.GreaterThan, new DateTime(2026, 1, 1)))
            .Build();

        Assert.Equal("2026-10-18", Exported(form, "start").GetProperty("default").GetString());
        Assert.Equal("2026-10-18T09:30:00", Exported(form, "at").GetProperty("default").GetString());
        Assert.Equal("14:30:00", Exported(form, "when").GetProperty("default").GetString());
        Assert.Equal("2026-01-02", Exported(form, "day").GetProperty("default").GetString());
        Assert.Equal("2026-01-01T00:00:00",
            Exported(form, "note").GetProperty("x-visibleWhen").GetProperty("value").GetString());

        // The date input shows what came back, which it could not do with "10/18/2026 00:00:00".
        var start = RoundTrip(form).FindField("start")!;
        Assert.Equal("2026-10-18", BlazorFormValueConverter.ToInputString(start.DefaultValue, start.Type));
    }

    [Fact]
    public void Every_integral_type_is_written_as_a_number()
    {
        var form = BlazorFormBuilder.Create()
            .Integer("small", f => f.Default((short)5).Attr("rows", (byte)3))
            .Integer("unsigned", f => f.Default(7u))
            .Build();

        Assert.Equal(5, Exported(form, "small").GetProperty("default").GetInt32());
        Assert.Equal(3, Exported(form, "small").GetProperty("x-attributes").GetProperty("rows").GetInt32());
        Assert.Equal(7, Exported(form, "unsigned").GetProperty("default").GetInt32());
        Assert.Equal(5L, RoundTrip(form).FindField("small")!.DefaultValue);
    }

    [Fact]
    public void Examples_offer_values_not_the_text_of_a_list()
    {
        // An example is a whole value, so a tag list's example is a list — and its members are what a
        // datalist should propose, not "System.Collections.Generic.List`1[System.Object]".
        const string json = """
        { "type": "object", "properties": {
            "t": { "type": "array", "x-widget": "tags", "examples": [["red", "blue"], ["red"]] },
            "s": { "type": "string", "examples": [true, 1.0, "x", {"no": 1}] } } }
        """;

        var form = Import(json);

        Assert.Equal(["red", "blue"], form.FindField("t")!.Suggestions);
        Assert.Equal(["true", "1", "x"], form.FindField("s")!.Suggestions);
    }

    // ---------------------------------------------------------------- conditions

    [Theory]
    [InlineData("neq")]
    [InlineData("!=")]
    [InlineData("1")]
    [InlineData("Equals, NotEquals")]
    public void An_operator_that_names_no_operator_makes_the_condition_unreadable(string op)
    {
        // It used to fall back to Equals, turning "hide when not X" into "hide when X".
        var json = $$"""
        { "type": "object", "properties": { "x": { "type": "string",
            "x-visibleWhen": { "field": "y", "op": "{{op}}", "value": "a" } } } }
        """;

        Assert.Null(Import(json).FindField("x")!.VisibleWhen);
    }

    [Fact]
    public void A_condition_without_an_op_still_means_equals_and_operator_names_ignore_case()
    {
        var form = Import("""
        { "type": "object", "properties": {
            "x": { "type": "string", "x-visibleWhen": { "field": "y", "value": "a" } },
            "z": { "type": "string", "x-visibleWhen": { "field": "y", "op": "notequals", "value": "a" } } } }
        """);

        Assert.Equal(BlazorFormConditionOperator.Equals, Assert.IsType<BlazorFormFieldCondition>(form.FindField("x")!.VisibleWhen).Operator);
        Assert.Equal(BlazorFormConditionOperator.NotEquals, Assert.IsType<BlazorFormFieldCondition>(form.FindField("z")!.VisibleWhen).Operator);
    }

    [Fact]
    public void A_group_with_an_unreadable_member_is_unreadable_as_a_whole()
    {
        // Keeping the readable half widens an `all` and narrows an `any` — the writer refuses to export
        // such a group, and the reader now refuses to import one.
        var form = Import("""
        { "type": "object", "properties": { "x": { "type": "string",
            "x-visibleWhen": { "all": [ { "field": "y", "op": "Equals", "value": "a" }, { "fld": "z" } ] } } } }
        """);

        Assert.Null(form.FindField("x")!.VisibleWhen);
    }

    // ---------------------------------------------------------------- formats and objects

    [Fact]
    public async Task A_uuid_format_and_a_pattern_are_both_enforced()
    {
        var form = Import("""
        { "type": "object", "properties": { "id": { "type": "string", "pattern": "^[a-f0-9-]+$", "format": "uuid" } } }
        """);

        Assert.NotEmpty(await Validate(form, ("id", "abc")));                                   // matches the pattern, not a UUID
        Assert.NotEmpty(await Validate(form, ("id", "0F8FAD5B-D9CB-469F-A165-70867728950E")));  // a UUID, not the pattern
        Assert.Empty(await Validate(form, ("id", "0f8fad5b-d9cb-469f-a165-70867728950e")));
    }

    [Fact]
    public async Task A_uuid_format_round_trips_as_a_format()
    {
        var form = Import("""{ "type": "object", "properties": { "id": { "type": "string", "format": "uuid" } } }""");

        var id = Exported(form, "id");
        Assert.Equal("uuid", id.GetProperty("format").GetString());
        Assert.False(id.TryGetProperty("pattern", out _));
        Assert.NotEmpty(await Validate(RoundTrip(form), ("id", "not-a-uuid")));
    }

    [Fact]
    public void An_object_fields_read_only_flag_survives()
    {
        var form = BlazorFormBuilder.Create()
            .Object("address", a => a.Text("city"))
            .Build();
        form.FindField("address")!.ReadOnly = true;

        Assert.True(RoundTrip(form).FindField("address")!.ReadOnly);
    }

    // ---------------------------------------------------------------- review: depth, recursion, cost

    /// <summary>A chain of definitions, each of which is nothing but an allOf of the next.</summary>
    private static string AllOfChain(int length, bool insideUnion = false)
    {
        var defs = new StringBuilder();
        for (var i = 0; i < length; i++)
        {
            defs.Append(insideUnion
                ? $"\"D{i}\":{{\"anyOf\":[{{\"allOf\":[{{\"$ref\":\"#/$defs/D{i + 1}\"}}]}},{{\"type\":\"null\"}}]}},"
                : $"\"D{i}\":{{\"allOf\":[{{\"$ref\":\"#/$defs/D{i + 1}\"}}]}},");
        }
        defs.Append($"\"D{length}\":{{\"type\":\"string\"}}");
        return "{\"$defs\":{" + defs + "},\"type\":\"object\",\"properties\":{\"x\":{\"$ref\":\"#/$defs/D0\"}}}";
    }

    /// <summary>
    /// Runs an import on a thread with a deliberately small stack. Every chain the importer follows has
    /// to be capped well inside it, or a stack overflow — which no catch can stop — ends the process.
    /// </summary>
    private static (bool Ok, string? Error, BlazorFormDefinition Form) ImportOnSmallStack(string json)
    {
        (bool, string?, BlazorFormDefinition) result = default;
        var thread = new Thread(() =>
        {
            var ok = BlazorFormJsonSchemaImporter.TryImport(json, out var form, out var error);
            result = (ok, error, form);
        }, maxStackSize: 256 * 1024);
        thread.Start();
        thread.Join();
        return result;
    }

    [Fact]
    public void A_chain_of_allOf_thousands_deep_is_refused_rather_than_overflowing_the_stack()
    {
        var (ok, error, _) = ImportOnSmallStack(AllOfChain(3000));

        Assert.False(ok);
        Assert.Contains("allOf", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_chain_of_allOf_inside_null_unions_terminates_on_a_small_stack()
    {
        var (ok, _, form) = ImportOnSmallStack(AllOfChain(3000, insideUnion: true));

        Assert.True(ok);
        Assert.NotNull(form.FindField("x"));
    }

    [Fact]
    public void A_short_chain_of_allOf_still_imports_on_a_small_stack()
    {
        var (ok, error, form) = ImportOnSmallStack(AllOfChain(40));

        Assert.True(ok, error);
        Assert.Equal(BlazorFormFieldType.Text, form.FindField("x")!.Type);
    }

    [Theory]
    [InlineData("""
        { "definitions": { "Node": { "type": "object", "properties": {
            "name": { "type": "string" },
            "next": { "description": "n", "allOf": [ { "$ref": "#/definitions/Node" } ] } } } },
          "type": "object",
          "properties": { "root": { "description": "r", "allOf": [ { "$ref": "#/definitions/Node" } ] } } }
        """)]
    [InlineData("""
        { "components": { "schemas": { "Node": { "type": "object", "properties": {
            "name": { "type": "string" },
            "next": { "allOf": [ { "$ref": "#/components/schemas/Node" } ], "nullable": true } } } } },
          "type": "object",
          "properties": { "root": { "allOf": [ { "$ref": "#/components/schemas/Node" } ], "nullable": true } } }
        """)]
    [InlineData("""
        { "$defs": { "Node": { "type": "object", "properties": {
            "name": { "type": "string" },
            "next": { "anyOf": [ { "allOf": [ { "$ref": "#/$defs/Node" } ] }, { "type": "null" } ] } } } },
          "type": "object",
          "properties": { "root": { "anyOf": [ { "allOf": [ { "$ref": "#/$defs/Node" } ] }, { "type": "null" } ] } } }
        """)]
    [InlineData("""
        { "$defs": {
            "Base": { "type": "object", "properties": { "next": { "$ref": "#/$defs/Node" } } },
            "Node": { "allOf": [ { "$ref": "#/$defs/Base" } ],
                      "properties": { "name": { "type": "string" }, "next": { "description": "n" } } } },
          "type": "object",
          "properties": { "root": { "allOf": [ { "$ref": "#/$defs/Node" } ] } } }
        """)]
    public void Recursion_spelled_through_allOf_is_cut_off_like_any_other(string json)
    {
        // pydantic v1 and Swashbuckle wrap every reference that carries a description in an allOf.
        // Those definitions never counted as the field's own, so the recursion ran on until the nesting
        // budget refused the whole document.
        Assert.True(BlazorFormJsonSchemaImporter.TryImport(json, out var form, out var error), error);

        var root = form.FindField("root")!;
        var next = root.Children.Single(c => c.Name == "next");
        Assert.Contains(next.Children, c => c.Name == "name");
        Assert.DoesNotContain(next.Children, c => c.Name == "next");
    }

    [Fact]
    public void A_definition_that_both_extends_and_contains_a_recursive_one_keeps_every_branch()
    {
        // An employee is a person, a person has a deputy who is a person, and an employee also has a
        // manager who is a person. Each of them is followed one level.
        const string json = """
        {
          "$defs": {
            "Person": { "type": "object", "properties": { "name": { "type": "string" }, "deputy": { "$ref": "#/$defs/Person" } } },
            "Employee": { "allOf": [ { "$ref": "#/$defs/Person" } ],
                          "properties": { "manager": { "$ref": "#/$defs/Person" } } }
          },
          "$ref": "#/$defs/Employee"
        }
        """;

        var form = Import(json);

        Assert.NotNull(form.FindField("name"));
        Assert.Equal(["name"], form.FindField("deputy")!.Children.Select(c => c.Name));
        Assert.Equal(["name"], form.FindField("manager")!.Children.Select(c => c.Name));
    }

    [Fact]
    public void A_wide_union_of_described_references_costs_almost_nothing_per_field()
    {
        // Each branch was resolved, merged with its title and materialised, several times per field:
        // 5,000 branches used by 300 fields took the better part of a minute.
        var json = new StringBuilder("""{"$defs":{"A":{"type":"object","properties":{"a":{"type":"string"}}},"U":{"anyOf":[""");
        for (var i = 0; i < 5000; i++) json.Append($"{{\"$ref\":\"#/$defs/A\",\"title\":\"t{i}\"}},");
        json.Length--;
        json.Append("]}},\"type\":\"object\",\"properties\":{");
        for (var f = 0; f < 300; f++) json.Append($"\"f{f}\":{{\"$ref\":\"#/$defs/U\"}},");
        json.Length--;
        json.Append("}}");

        var clock = System.Diagnostics.Stopwatch.StartNew();
        Assert.True(BlazorFormJsonSchemaImporter.TryImport(json.ToString(), out var form, out var error), error);

        Assert.Equal(300, form.Fields.Count);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3), $"took {clock.Elapsed}");
    }

    [Fact]
    public void Ordinary_use_of_allOf_on_thousands_of_fields_stays_inside_the_merge_budget()
    {
        var json = new StringBuilder("""{"$defs":{"A":{"minLength":1},"B":{"maxLength":50},"C":{"title":"t"},"D":{"type":"string"}},"type":"object","properties":{""");
        for (var i = 0; i < 3000; i++)
            json.Append($"\"f{i}\":{{\"allOf\":[{{\"$ref\":\"#/$defs/A\"}},{{\"$ref\":\"#/$defs/B\"}},{{\"$ref\":\"#/$defs/C\"}},{{\"$ref\":\"#/$defs/D\"}}]}},");
        json.Length--;
        json.Append("}}");

        Assert.True(BlazorFormJsonSchemaImporter.TryImport(json.ToString(), out var form, out var error), error);
        Assert.Equal(3000, form.Fields.Count);
        Assert.Equal(50, form.Fields[0].MaxLength);
    }

    [Fact]
    public void An_allOf_that_doubles_at_every_level_runs_out_of_merges()
    {
        var json = new StringBuilder("""{"$defs":{"L0":{"type":"string"},""");
        for (var i = 1; i <= 25; i++)
            json.Append($"\"L{i}\":{{\"allOf\":[{{\"$ref\":\"#/$defs/L{i - 1}\"}},{{\"$ref\":\"#/$defs/L{i - 1}\"}}]}},");
        json.Length--;
        json.Append("""},"type":"object","properties":{"x":{"$ref":"#/$defs/L25"}}}""");

        Assert.False(BlazorFormJsonSchemaImporter.TryImport(json.ToString(), out _, out var error));
        Assert.Contains("merges", error, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- review: typed choices

    [Fact]
    public void A_typed_choice_whose_options_are_not_canonical_numbers_stays_text()
    {
        // "1.50" written as 1.5 would be a different option; written as "1.50" under "type": "number"
        // the schema contradicts itself, and the re-import stored 1.5 — which matched no option.
        var rate = new BlazorFormFieldDefinition("rate", BlazorFormFieldType.Select) { ValueType = typeof(decimal?) };
        rate.Options.Add(new BlazorFormSelectOption("0.50", "50c"));
        rate.Options.Add(new BlazorFormSelectOption("1.50", "1.50"));
        var form = new BlazorFormDefinition();
        form.Fields.Add(rate);

        var exported = Exported(form, "rate");
        Assert.Equal("string", exported.GetProperty("type").GetString());
        Assert.Equal(["0.50", "1.50"], exported.GetProperty("enum").EnumerateArray().Select(e => e.GetString()));

        var back = RoundTrip(form).FindField("rate")!;
        var picked = BlazorFormValueConverter.FromInputString("1.50", back.ValueType, back.Type);
        Assert.Contains(back.Options, o => o.Value == BlazorFormValueConverter.ToInputString(picked, back.Type));
    }

    [Fact]
    public void A_declared_type_the_enum_contradicts_is_not_believed()
    {
        var field = Import("""
        { "type": "object", "properties": { "n": { "type": "integer", "enum": ["007", 1] } } }
        """).FindField("n")!;

        Assert.Null(field.ValueType);
        Assert.Equal(["007", "1"], field.Options.Select(o => o.Value));
    }

    [Fact]
    public void A_typed_choice_fed_by_a_provider_keeps_its_type_across_round_trips()
    {
        var level = new BlazorFormFieldDefinition("level", BlazorFormFieldType.Select) { ValueType = typeof(int?) };
        var ids = new BlazorFormFieldDefinition("ids", BlazorFormFieldType.MultiSelect) { ValueType = typeof(List<int>) };
        var form = new BlazorFormDefinition();
        form.Fields.Add(level);
        form.Fields.Add(ids);

        var reimported = RoundTrip(form);
        var second = BlazorFormJsonSchemaExporter.Export(reimported);
        var third = BlazorFormJsonSchemaExporter.Export(Import(second));

        Assert.Equal("integer", Exported(form, "level").GetProperty("type").GetString());
        Assert.Equal("integer", Exported(form, "ids").GetProperty("items").GetProperty("type").GetString());
        Assert.Equal(typeof(long?), reimported.FindField("level")!.ValueType);
        Assert.Equal(typeof(List<long>), reimported.FindField("ids")!.ValueType);
        Assert.Equal(second, third);
    }

    // ---------------------------------------------------------------- review: merging

    [Fact]
    public void A_draft04_exclusive_bound_is_folded_into_its_own_branch_before_merging()
    {
        var form = Import("""
        { "type": "object", "properties": {
            "n": { "type": "number", "allOf": [ { "minimum": 5, "exclusiveMinimum": true }, { "minimum": 10 } ] },
            "m": { "type": "number", "allOf": [ { "minimum": 5, "exclusiveMinimum": true } ] } } }
        """);

        var n = Assert.Single(form.FindField("n")!.Validators.OfType<BlazorFormRangeRule>());
        Assert.Equal(10, n.Min);
        Assert.False(n.MinExclusive);

        var m = Assert.Single(form.FindField("m")!.Validators.OfType<BlazorFormRangeRule>());
        Assert.Equal(5, m.Min);
        Assert.True(m.MinExclusive);
    }

    [Fact]
    public async Task Two_patterns_in_an_allOf_are_both_enforced_and_survive_a_round_trip()
    {
        var form = Import("""
        { "type": "object", "properties": { "p": { "type": "string", "allOf": [ { "pattern": "^a" }, { "pattern": "z$" } ] } } }
        """);

        Assert.NotEmpty(await Validate(form, ("p", "abc")));
        Assert.NotEmpty(await Validate(form, ("p", "xyz")));
        Assert.Empty(await Validate(form, ("p", "abz")));

        var once = BlazorFormJsonSchemaExporter.Export(form);
        var back = Import(once);
        Assert.Equal(once, BlazorFormJsonSchemaExporter.Export(back));
        Assert.NotEmpty(await Validate(back, ("p", "abc")));
        Assert.NotEmpty(await Validate(back, ("p", "xyz")));
    }

    [Fact]
    public void Two_enums_in_an_allOf_keep_only_what_both_allow_with_their_labels()
    {
        var p = Import("""
        { "type": "object", "properties": { "p": { "allOf": [
            { "enum": ["a", "b", "c"], "enumNames": ["A", "B", "C"] },
            { "enum": ["b", "c", "d"] } ] } } }
        """).FindField("p")!;

        Assert.Equal(["b", "c"], p.Options.Select(o => o.Value));
        Assert.Equal(["B", "C"], p.Options.Select(o => o.Label));
    }

    [Fact]
    public void Item_schemas_from_two_branches_are_both_applied()
    {
        var t = Import("""
        { "type": "object", "properties": { "t": { "type": "array", "x-widget": "tags", "allOf": [
            { "items": { "type": "string", "maxLength": 5 } },
            { "items": { "examples": ["a"] } } ] } } }
        """).FindField("t")!;

        Assert.Equal(5, t.MaxLength);
        Assert.Equal(["a"], t.Suggestions);
    }

    [Fact]
    public void A_condition_comparing_with_a_number_that_has_no_json_form_is_omitted()
    {
        // Written as null, "less than infinity" became "never".
        var form = BlazorFormBuilder.Create()
            .Text("a", f => f.VisibleWhen("b", BlazorFormConditionOperator.LessThan, double.PositiveInfinity))
            .Build();

        Assert.False(Exported(form, "a").TryGetProperty("x-visibleWhen", out _));
        Assert.False(BlazorFormConditionJson.CanWrite(form.FindField("a")!.VisibleWhen));
    }
}
