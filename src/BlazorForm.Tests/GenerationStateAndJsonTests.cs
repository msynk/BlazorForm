using BlazorForm.Core.Building;
using BlazorForm.Core.Data;
using BlazorForm.Core.Generation;
using BlazorForm.Core.Json;
using BlazorForm.Core.Schema;
using BlazorForm.Core.State;

namespace BlazorForm.Tests;

public class SchemaGeneratorTests
{
    [Fact]
    public void Generates_fields_from_model()
    {
        var form = SchemaGenerator.Generate<RegistrationModel>();

        Assert.Equal("First name", form.FindField("FirstName")!.Label); // from [Display]
        Assert.True(form.FindField("FirstName")!.Required);
        Assert.Equal(FieldType.Email, form.FindField("Email")!.Type);
        Assert.Equal(FieldType.Select, form.FindField("AccountType")!.Type);
        Assert.Equal(2, form.FindField("AccountType")!.Options.Count);
        Assert.Equal(FieldType.Object, form.FindField("Address")!.Type);
        Assert.Equal(FieldType.Array, form.FindField("Items")!.Type);
        Assert.NotNull(form.FindField("Items")!.ItemTemplate);
    }

    [Fact]
    public void Generated_range_and_length_produce_constraints()
    {
        var form = SchemaGenerator.Generate<RegistrationModel>();
        var age = form.FindField("Age")!;
        Assert.Equal(18, age.Min);
        Assert.Equal(120, age.Max);
        Assert.Equal(50, form.FindField("FirstName")!.MaxLength);
    }
}

public class FormBuilderTests
{
    [Fact]
    public void Builds_typed_form_with_inferred_types()
    {
        var form = FormBuilder.For<RegistrationModel>()
            .Title("Register")
            .Field(x => x.FirstName, f => f.Required().MinLength(2))
            .Field(x => x.Age, f => f.Range(18, 120))
            .Field(x => x.AccountType)
            .Build();

        Assert.Equal("Register", form.Title);
        Assert.Equal(FieldType.Integer, form.FindField("Age")!.Type);
        Assert.Equal(FieldType.Select, form.FindField("AccountType")!.Type);
        Assert.Equal(2, form.FindField("AccountType")!.Options.Count);
    }

    [Fact]
    public void Untyped_builder_supports_objects_and_arrays_and_steps()
    {
        var form = FormBuilder.Create()
            .Text("name", f => f.Required())
            .Object("address", a => a.Text("city"))
            .Array("items", i => i.Text("product").Number("qty"))
            .Step("s1", s => s.Title("Step 1").Fields("name"))
            .Build();

        Assert.True(form.IsWizard);
        Assert.Equal(FieldType.Object, form.FindField("address")!.Type);
        Assert.Equal(FieldType.Array, form.FindField("items")!.Type);
        Assert.Equal(2, form.FindField("items")!.ItemTemplate!.Children.Count);
    }
}

public class FormStateTests
{
    [Fact]
    public void Tracks_dirty_and_value_changes()
    {
        var form = SchemaGenerator.Generate<RegistrationModel>();
        var state = new FormState(form, new ModelDataAccessor(new RegistrationModel()));

        Assert.False(state.IsFormDirty);
        state.SetValue("FirstName", "Grace");
        Assert.True(state.IsFormDirty);
        Assert.Equal("Grace", state.GetValue("FirstName"));
    }

    [Fact]
    public void Array_add_and_remove_works_on_typed_model()
    {
        var form = SchemaGenerator.Generate<RegistrationModel>();
        var model = new RegistrationModel();
        var state = new FormState(form, new ModelDataAccessor(model));
        var itemsField = form.FindField("Items")!;

        var idx = state.AddArrayItem(itemsField, "Items");
        Assert.Equal(0, idx);
        Assert.Single(model.Items);
        Assert.IsType<LineItem>(model.Items[0]);

        state.SetValue("Items[0].Product", "Widget");
        Assert.Equal("Widget", model.Items[0].Product);

        state.RemoveArrayItem("Items", 0);
        Assert.Empty(model.Items);
    }

    [Fact]
    public async Task Wizard_navigation_validates_each_step()
    {
        var form = FormBuilder.For<RegistrationModel>()
            .Field(x => x.FirstName, f => f.Required())
            .Field(x => x.Email, f => f.Required().Email())
            .Step("s1", s => s.Fields("FirstName"))
            .Step("s2", s => s.Fields("Email"))
            .Build();

        var model = new RegistrationModel();
        var state = new FormState(form, new ModelDataAccessor(model));

        // Cannot advance while first step invalid.
        Assert.False(await state.NextStepAsync());
        Assert.Equal(0, state.CurrentStepIndex);

        state.SetValue("FirstName", "Ada");
        Assert.True(await state.NextStepAsync());
        Assert.Equal(1, state.CurrentStepIndex);
        Assert.True(state.IsLastStep);
    }

    [Fact]
    public void Applies_default_values()
    {
        var form = FormBuilder.Create()
            .Checkbox("subscribe", f => f.Default(true))
            .Build();
        var state = new FormState(form, new DictionaryDataAccessor());
        Assert.Equal(true, state.GetValue("subscribe"));
    }
}

public class JsonSchemaTests
{
    private const string Json = """
    {
      "title": "Contact",
      "type": "object",
      "required": ["name", "email"],
      "properties": {
        "name": { "type": "string", "title": "Full name", "minLength": 2 },
        "email": { "type": "string", "format": "email" },
        "age": { "type": "integer", "minimum": 0, "maximum": 120 },
        "role": { "type": "string", "enum": ["admin", "user"], "enumNames": ["Admin", "User"] },
        "bio": { "type": "string", "x-widget": "textarea" },
        "address": {
          "type": "object",
          "properties": { "city": { "type": "string" } }
        },
        "tags": { "type": "array", "items": { "type": "string" } }
      }
    }
    """;

    [Fact]
    public void Imports_json_schema()
    {
        var form = JsonSchemaImporter.Import(Json);

        Assert.Equal("Contact", form.Title);
        Assert.True(form.FindField("name")!.Required);
        Assert.Equal("Full name", form.FindField("name")!.Label);
        Assert.Equal(FieldType.Email, form.FindField("email")!.Type);
        Assert.Equal(FieldType.Integer, form.FindField("age")!.Type);
        Assert.Equal(FieldType.Select, form.FindField("role")!.Type);
        Assert.Equal("Admin", form.FindField("role")!.Options[0].Label);
        Assert.Equal(FieldType.TextArea, form.FindField("bio")!.Type);
        Assert.Equal(FieldType.Object, form.FindField("address")!.Type);
        Assert.Equal(FieldType.Array, form.FindField("tags")!.Type);
    }

    [Fact]
    public void Round_trips_through_export_and_import()
    {
        var original = JsonSchemaImporter.Import(Json);
        var exported = JsonSchemaExporter.Export(original);
        var reimported = JsonSchemaImporter.Import(exported);

        Assert.Equal(original.Fields.Count, reimported.Fields.Count);
        Assert.Equal(FieldType.Email, reimported.FindField("email")!.Type);
        Assert.Equal(FieldType.Select, reimported.FindField("role")!.Type);
        Assert.True(reimported.FindField("name")!.Required);
        Assert.Equal(FieldType.Object, reimported.FindField("address")!.Type);
    }
}
