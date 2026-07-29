namespace BlazorForm.Tests;

public class JsonSchemaImportTests
{
    [Fact]
    public void Resolves_local_refs_into_defs()
    {
        const string json = """
        {
          "type": "object",
          "properties": {
            "home": { "$ref": "#/$defs/address" },
            "work": { "$ref": "#/$defs/address" }
          },
          "$defs": {
            "address": {
              "type": "object",
              "title": "Address",
              "required": ["city"],
              "properties": {
                "street": { "type": "string" },
                "city":   { "type": "string" }
              }
            }
          }
        }
        """;

        var form = BlazorFormJsonSchemaImporter.Import(json);

        var home = form.FindField("home")!;
        Assert.Equal(BlazorFormFieldType.Object, home.Type);
        Assert.Equal(2, home.Children.Count);
        Assert.True(home.Children.Single(c => c.Name == "city").Required);
        Assert.Equal(2, form.FindField("work")!.Children.Count);
    }

    [Fact]
    public void A_reference_cycle_terminates_instead_of_recursing_forever()
    {
        const string json = """
        {
          "type": "object",
          "properties": { "node": { "$ref": "#/$defs/loop" } },
          "$defs": { "loop": { "$ref": "#/$defs/loop" } }
        }
        """;

        var form = BlazorFormJsonSchemaImporter.Import(json);
        Assert.NotNull(form.FindField("node"));
    }

    [Fact]
    public void Merges_allOf_branches_into_one_field_set()
    {
        const string json = """
        {
          "allOf": [
            { "type": "object", "required": ["id"], "properties": { "id": { "type": "string" } } },
            { "type": "object", "properties": { "name": { "type": "string", "minLength": 2 } } }
          ],
          "type": "object",
          "title": "Merged",
          "required": ["name"],
          "properties": { "email": { "type": "string", "format": "email" } }
        }
        """;

        var form = BlazorFormJsonSchemaImporter.Import(json);

        Assert.Equal("Merged", form.Title);
        Assert.Equal(3, form.Fields.Count);
        Assert.True(form.FindField("id")!.Required);
        Assert.True(form.FindField("name")!.Required);
        Assert.Equal(2, form.FindField("name")!.MinLength);
        Assert.Equal(BlazorFormFieldType.Email, form.FindField("email")!.Type);
    }

    [Fact]
    public void Nullable_type_unions_resolve_to_the_non_null_type()
    {
        const string json = """
        { "type": "object", "properties": { "age": { "type": ["integer", "null"] } } }
        """;

        Assert.Equal(BlazorFormFieldType.Integer, BlazorFormJsonSchemaImporter.Import(json).FindField("age")!.Type);
    }

    [Fact]
    public async Task Exclusive_bounds_reject_the_boundary_value()
    {
        const string json = """
        {
          "type": "object",
          "properties": { "count": { "type": "integer", "exclusiveMinimum": 0, "exclusiveMaximum": 10 } }
        }
        """;

        var form = BlazorFormJsonSchemaImporter.Import(json);
        var data = new BlazorFormDictionaryDataAccessor();
        var validator = new BlazorFormValidator();

        data.SetValue("count", 0);
        Assert.NotEmpty(await validator.ValidateAsync(form, data));

        data.SetValue("count", 1);
        Assert.Empty(await validator.ValidateAsync(form, data));

        data.SetValue("count", 10);
        Assert.NotEmpty(await validator.ValidateAsync(form, data));
    }

    [Fact]
    public async Task MultipleOf_and_uniqueItems_become_rules()
    {
        const string json = """
        {
          "type": "object",
          "properties": {
            "quantity": { "type": "number", "multipleOf": 5 },
            "tags": { "type": "array", "uniqueItems": true, "items": { "type": "string" } }
          }
        }
        """;

        var form = BlazorFormJsonSchemaImporter.Import(json);
        var data = new BlazorFormDictionaryDataAccessor();
        data.SetValue("quantity", 7);
        data.SetValue("tags", new List<object?> { "a", "a" });

        var messages = await new BlazorFormValidator().ValidateAsync(form, data);

        Assert.Contains(messages, m => m.FieldPath == "quantity");
        Assert.Contains(messages, m => m.FieldPath == "tags");
    }

    [Fact]
    public void Const_pins_a_value_and_makes_the_field_read_only()
    {
        const string json = """
        { "type": "object", "properties": { "kind": { "type": "string", "const": "invoice" } } }
        """;

        var kind = BlazorFormJsonSchemaImporter.Import(json).FindField("kind")!;
        Assert.Equal("invoice", kind.DefaultValue);
        Assert.True(kind.ReadOnly);
    }

    [Fact]
    public void ReadOnly_and_extra_formats_are_honoured()
    {
        const string json = """
        {
          "type": "object",
          "properties": {
            "id":     { "type": "string", "format": "uuid", "readOnly": true },
            "avatar": { "type": "string", "contentEncoding": "base64", "contentMediaType": "image/png" },
            "site":   { "type": "string", "format": "uri" }
          }
        }
        """;

        var form = BlazorFormJsonSchemaImporter.Import(json);

        Assert.True(form.FindField("id")!.ReadOnly);
        Assert.Equal(BlazorFormFieldType.File, form.FindField("avatar")!.Type);
        Assert.Equal(BlazorFormFieldType.Url, form.FindField("site")!.Type);
        Assert.Contains(form.FindField("site")!.Validators, v => v.Key == "url");
    }

    [Fact]
    public async Task DependentRequired_becomes_conditional_requiredness()
    {
        const string json = """
        {
          "type": "object",
          "properties": {
            "creditCard": { "type": "string" },
            "billingAddress": { "type": "string" }
          },
          "dependentRequired": { "creditCard": ["billingAddress"] }
        }
        """;

        var form = BlazorFormJsonSchemaImporter.Import(json);
        var data = new BlazorFormDictionaryDataAccessor();
        var validator = new BlazorFormValidator();

        // No card, no obligation.
        Assert.Empty(await validator.ValidateAsync(form, data));

        data.SetValue("creditCard", "4111111111111111");
        var messages = await validator.ValidateAsync(form, data);
        Assert.Contains(messages, m => m.FieldPath == "billingAddress");
    }

    [Fact]
    public async Task If_then_else_becomes_conditional_requiredness()
    {
        const string json = """
        {
          "type": "object",
          "properties": {
            "kind":        { "type": "string", "enum": ["personal", "business"] },
            "companyName": { "type": "string" },
            "nickname":    { "type": "string" }
          },
          "if":   { "properties": { "kind": { "const": "business" } } },
          "then": { "required": ["companyName"] },
          "else": { "required": ["nickname"] }
        }
        """;

        var form = BlazorFormJsonSchemaImporter.Import(json);
        var data = new BlazorFormDictionaryDataAccessor();
        var validator = new BlazorFormValidator();

        data.SetValue("kind", "business");
        var business = await validator.ValidateAsync(form, data);
        Assert.Contains(business, m => m.FieldPath == "companyName");
        Assert.DoesNotContain(business, m => m.FieldPath == "nickname");

        data.SetValue("kind", "personal");
        var personal = await validator.ValidateAsync(form, data);
        Assert.Contains(personal, m => m.FieldPath == "nickname");
        Assert.DoesNotContain(personal, m => m.FieldPath == "companyName");
    }

    [Fact]
    public void Conditions_travel_in_x_extensions()
    {
        const string json = """
        {
          "type": "object",
          "properties": {
            "isBusiness": { "type": "boolean" },
            "companyName": {
              "type": "string",
              "x-clearOnHide": true,
              "x-visibleWhen": { "field": "isBusiness", "op": "IsTrue" },
              "x-requiredWhen": { "field": "isBusiness", "op": "IsTrue" }
            },
            "vat": {
              "type": "string",
              "x-disabledWhen": { "any": [
                 { "field": "isBusiness", "op": "IsFalse" },
                 { "field": "companyName", "op": "IsEmpty" }
              ] }
            }
          }
        }
        """;

        var form = BlazorFormJsonSchemaImporter.Import(json);
        var company = form.FindField("companyName")!;

        Assert.True(company.ClearOnHide);
        Assert.NotNull(company.VisibleWhen);
        Assert.NotNull(company.RequiredWhen);

        var vat = form.FindField("vat")!;
        var group = Assert.IsType<BlazorFormConditionGroup>(vat.DisabledWhen);
        Assert.Equal(BlazorFormConditionLogic.Or, group.Logic);
        Assert.Equal(2, group.Conditions.Count);
    }

    [Fact]
    public void Wizard_steps_travel_in_x_steps()
    {
        const string json = """
        {
          "type": "object",
          "properties": { "a": { "type": "string" }, "b": { "type": "string" } },
          "x-steps": [
            { "id": "one", "title": "First", "fields": ["a"] },
            { "id": "two", "title": "Second", "fields": ["b"],
              "visibleWhen": { "field": "a", "op": "IsNotEmpty" } }
          ]
        }
        """;

        var form = BlazorFormJsonSchemaImporter.Import(json);

        Assert.True(form.IsWizard);
        Assert.Equal(2, form.Steps.Count);
        Assert.Equal("First", form.Steps[0].Title);
        Assert.NotNull(form.Steps[1].VisibleWhen);
    }

    [Fact]
    public void Malformed_json_is_reported_rather_than_thrown()
    {
        Assert.False(BlazorFormJsonSchemaImporter.TryImport("{ not json", out _, out var error));
        Assert.NotNull(error);

        Assert.True(BlazorFormJsonSchemaImporter.TryImport("""{"type":"object","properties":{}}""", out var form, out _));
        Assert.Empty(form.Fields);
    }
}

public class JsonSchemaRoundTripTests
{
    [Fact]
    public void A_built_form_survives_export_and_import()
    {
        var original = BlazorFormBuilder.Create()
            .Title("Order")
            .Description("Place an order")
            .Columns(2)
            .Text("customer", f => f.Required().Placeholder("Who is ordering?").Autocomplete("name").ColumnSpan(2))
            .Select("kind", f => f.Options(("personal", "Personal"), ("business", "Business")))
            .Text("companyName", f => f
                .VisibleWhen("kind", BlazorFormConditionOperator.Equals, "business")
                .RequiredWhen("kind", BlazorFormConditionOperator.Equals, "business")
                .ClearOnHide())
            .Integer("quantity", f => f.Range(1, 99).Step(1))
            .Field("notes", BlazorFormFieldType.TextArea, f => f.ReadOnly())
            .ArrayOf("tags", BlazorFormFieldType.Text, f => f.Items(min: 1, max: 3).UniqueItems())
            .Object("address", a => a.Text("city", c => c.Required()).Text("zip"))
            .Step("s1", s => s.Title("Who").Fields("customer", "kind"))
            .Step("s2", s => s.Title("What").Fields("quantity", "tags")
                .VisibleWhen(new BlazorFormFieldCondition("kind", BlazorFormConditionOperator.IsNotEmpty)))
            .Build();

        var json = BlazorFormJsonSchemaExporter.Export(original);
        var reimported = BlazorFormJsonSchemaImporter.Import(json);

        Assert.Equal(original.Title, reimported.Title);
        Assert.Equal(original.Description, reimported.Description);
        Assert.Equal(2, reimported.Columns);
        Assert.Equal(original.Fields.Count, reimported.Fields.Count);

        var customer = reimported.FindField("customer")!;
        Assert.True(customer.Required);
        Assert.Equal("Who is ordering?", customer.Placeholder);
        Assert.Equal("name", customer.Autocomplete);
        Assert.Equal(2, customer.ColumnSpan);

        var company = reimported.FindField("companyName")!;
        Assert.True(company.ClearOnHide);
        var visible = Assert.IsType<BlazorFormFieldCondition>(company.VisibleWhen);
        Assert.Equal("kind", visible.FieldPath);
        Assert.Equal(BlazorFormConditionOperator.Equals, visible.Operator);
        Assert.Equal("business", visible.Value);
        Assert.NotNull(company.RequiredWhen);

        var quantity = reimported.FindField("quantity")!;
        Assert.Equal(BlazorFormFieldType.Integer, quantity.Type);
        Assert.Equal(1, quantity.Min);
        Assert.Equal(99, quantity.Max);

        Assert.Equal(BlazorFormFieldType.TextArea, reimported.FindField("notes")!.Type);
        Assert.True(reimported.FindField("notes")!.ReadOnly);

        var tags = reimported.FindField("tags")!;
        Assert.Equal(1, tags.MinItems);
        Assert.Equal(3, tags.MaxItems);
        Assert.Contains(tags.Validators, v => v.Key == "uniqueItems");

        var address = reimported.FindField("address")!;
        Assert.Equal(BlazorFormFieldType.Object, address.Type);
        Assert.True(address.Children.Single(c => c.Name == "city").Required);

        Assert.True(reimported.IsWizard);
        Assert.Equal(2, reimported.Steps.Count);
        Assert.Equal("Who", reimported.Steps[0].Title);
        Assert.Equal(["customer", "kind"], reimported.Steps[0].Fields);
        Assert.NotNull(reimported.Steps[1].VisibleWhen);
    }

    [Fact]
    public void A_generated_schema_survives_export_and_import()
    {
        var original = BlazorFormSchemaGenerator.Generate<RegistrationModel>();
        var reimported = BlazorFormJsonSchemaImporter.Import(BlazorFormJsonSchemaExporter.Export(original));

        Assert.Equal(original.Fields.Count, reimported.Fields.Count);
        Assert.Equal(BlazorFormFieldType.Email, reimported.FindField("Email")!.Type);
        Assert.Equal(BlazorFormFieldType.Select, reimported.FindField("AccountType")!.Type);
        Assert.Equal(BlazorFormFieldType.Object, reimported.FindField("Address")!.Type);
        Assert.Equal(BlazorFormFieldType.Array, reimported.FindField("Items")!.Type);
        Assert.Equal(2, reimported.FindField("Items")!.ItemTemplate!.Children.Count);
    }

    [Fact]
    public void Delegate_backed_conditions_are_omitted_rather_than_approximated()
    {
        var form = BlazorFormBuilder.Create()
            .Text("a")
            .Text("b", f => f.VisibleWhen(data => data.GetValue("a") is not null, "a"))
            .Build();

        var json = BlazorFormJsonSchemaExporter.Export(form);

        Assert.DoesNotContain("x-visibleWhen", json, StringComparison.Ordinal);
        Assert.Null(BlazorFormJsonSchemaImporter.Import(json).FindField("b")!.VisibleWhen);
    }

    [Fact]
    public void Numeric_enum_values_keep_their_type()
    {
        var form = BlazorFormBuilder.Create()
            .Integer("level", f => f.Options(("1", "Low"), ("2", "High")))
            .Build();
        // Options() flips the field to Select; force it back so the export uses the integer type.
        form.FindField("level")!.Type = BlazorFormFieldType.Integer;

        var json = BlazorFormJsonSchemaExporter.Export(form);

        Assert.Contains("\"enum\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"1\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void The_export_declares_its_schema_version()
        => Assert.Contains("json-schema.org", BlazorFormJsonSchemaExporter.Export(BlazorFormBuilder.Create().Text("a").Build()),
            StringComparison.Ordinal);
}
