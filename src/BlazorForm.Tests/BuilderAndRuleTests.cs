namespace BlazorForm.Tests;

public class RuleDeduplicationTests
{
    [Fact]
    public async Task A_model_annotation_refined_by_the_builder_reports_once()
    {
        // FirstName carries [Required] and [StringLength(50, MinimumLength = 2)]. Restating those in
        // the builder used to stack a second copy of each rule and show the user two identical errors.
        var form = BlazorFormBuilder.For<RegistrationModel>()
            .Field(x => x.FirstName, f => f.Required("Please enter your first name.").MinLength(2))
            .Build();

        var messages = await new BlazorFormValidator()
            .ValidateAsync(form, new BlazorFormModelDataAccessor(new RegistrationModel()));

        var firstName = messages.Where(m => m.FieldPath == "FirstName").ToList();
        Assert.Single(firstName);
        Assert.Equal("Please enter your first name.", firstName[0].Message);
    }

    [Fact]
    public void The_last_message_wins_for_a_repeated_rule()
    {
        var field = new BlazorFormFieldDefinition("x", BlazorFormFieldType.Text);
        new BlazorFormFieldBuilder(field).Required("first").Required("second");

        Assert.Single(field.Validators);
    }

    [Fact]
    public void Custom_rules_without_a_key_still_accumulate()
    {
        var field = new BlazorFormFieldDefinition("x", BlazorFormFieldType.Text);
        new BlazorFormFieldBuilder(field)
            .Must(v => v is not null, "one")
            .Must(v => v is string, "two");

        Assert.Equal(2, field.Validators.Count);
    }
}

public class BuiltInRuleBehaviourTests
{
    private static BlazorFormValidationContext Ctx(object? value, IBlazorFormDataReader? data = null)
        => new("f", value, data ?? new BlazorFormDictionaryDataAccessor());

    [Fact]
    public async Task Url_rule_accepts_absolute_http_urls_only()
    {
        var rule = new BlazorFormUrlRule();
        Assert.True((await rule.ValidateAsync(Ctx("https://example.com"))).IsValid);
        Assert.True((await rule.ValidateAsync(Ctx(""))).IsValid);
        Assert.False((await rule.ValidateAsync(Ctx("example.com"))).IsValid);
        Assert.False((await rule.ValidateAsync(Ctx("javascript:alert(1)"))).IsValid);
    }

    [Fact]
    public async Task Compare_rule_matches_another_field()
    {
        var data = new BlazorFormDictionaryDataAccessor();
        data.SetValue("Password", "hunter2");
        var rule = new BlazorFormCompareRule("Password", "Password");

        Assert.True((await rule.ValidateAsync(Ctx("hunter2", data))).IsValid);
        Assert.False((await rule.ValidateAsync(Ctx("hunter3", data))).IsValid);
        // An empty value is the required rule's business, not this one's.
        Assert.True((await rule.ValidateAsync(Ctx("", data))).IsValid);
    }

    [Fact]
    public async Task MultipleOf_tolerates_binary_floating_point()
    {
        var rule = new BlazorFormMultipleOfRule(0.1);
        Assert.True((await rule.ValidateAsync(Ctx(0.3))).IsValid);
        Assert.False((await rule.ValidateAsync(Ctx(0.35))).IsValid);
    }

    [Fact]
    public async Task UniqueItems_rejects_duplicates()
    {
        var rule = new BlazorFormUniqueItemsRule();
        Assert.True((await rule.ValidateAsync(Ctx(new List<string> { "a", "b" }))).IsValid);
        Assert.False((await rule.ValidateAsync(Ctx(new List<string> { "a", "a" }))).IsValid);
    }

    [Fact]
    public async Task An_invalid_pattern_is_treated_as_no_constraint_rather_than_throwing()
    {
        // Patterns arrive from untrusted JSON Schema documents; one that does not compile must not
        // take the import (or the render) down.
        var rule = new BlazorFormPatternRule("([unclosed");

        Assert.False(rule.IsPatternValid);
        Assert.True((await rule.ValidateAsync(Ctx("anything"))).IsValid);
    }

    [Fact]
    public async Task Range_message_names_both_bounds()
    {
        var result = await new BlazorFormRangeRule(1, 10).ValidateAsync(Ctx(50));
        Assert.Contains("1", result.Message, StringComparison.Ordinal);
        Assert.Contains("10", result.Message, StringComparison.Ordinal);
    }
}

public class MessageProviderTests
{
    private sealed class Dutch : IBlazorFormMessageProvider
    {
        public string Get(string key, params object?[] args) => key switch
        {
            BlazorFormMessageKeys.Required => "Dit veld is verplicht.",
            _ => key
        };
    }

    private sealed class Services(object service) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(IBlazorFormMessageProvider) ? service : null;
    }

    [Fact]
    public async Task A_registered_provider_replaces_the_built_in_english()
    {
        var ctx = new BlazorFormValidationContext("f", null, new BlazorFormDictionaryDataAccessor(), new Services(new Dutch()));
        var result = await new BlazorFormRequiredRule().ValidateAsync(ctx);

        Assert.Equal("Dit veld is verplicht.", result.Message);
    }

    [Fact]
    public async Task An_explicit_message_still_wins_over_the_provider()
    {
        var ctx = new BlazorFormValidationContext("f", null, new BlazorFormDictionaryDataAccessor(), new Services(new Dutch()));
        var result = await new BlazorFormRequiredRule("Custom").ValidateAsync(ctx);

        Assert.Equal("Custom", result.Message);
    }

    [Fact]
    public async Task Without_a_provider_the_english_defaults_apply()
    {
        var ctx = new BlazorFormValidationContext("f", null, new BlazorFormDictionaryDataAccessor());
        var result = await new BlazorFormRequiredRule().ValidateAsync(ctx);

        Assert.Equal("This field is required.", result.Message);
    }
}

public class TypedBuilderTests
{
    [Fact]
    public void A_nested_member_expression_becomes_a_dotted_path()
    {
        var form = BlazorFormBuilder.For<RegistrationModel>()
            .Field(x => x.Address.City, f => f.Required())
            .Build();

        var field = form.Fields.Single();
        Assert.Equal("Address.City", field.Name);
        Assert.Equal("City", field.Label);
    }

    [Fact]
    public async Task A_nested_member_field_reads_and_validates_through_its_path()
    {
        var form = BlazorFormBuilder.For<RegistrationModel>()
            .Field(x => x.Address.City, f => f.Required("City is required."))
            .Build();

        var model = new RegistrationModel();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        Assert.False(await state.ValidateAsync());
        Assert.Equal("City is required.", state.MessagesFor("Address.City")[0].Message);

        state.SetValue("Address.City", "Paris");
        Assert.Equal("Paris", model.Address.City);
        Assert.True(await state.ValidateAsync());
    }

    [Fact]
    public void Typed_object_builder_configures_children_against_the_child_type()
    {
        var form = BlazorFormBuilder.For<RegistrationModel>()
            .Object(x => x.Address, a => a
                .Field(x => x.Street)
                .Field(x => x.City, f => f.Label("Town")))
            .Build();

        var address = form.FindField("Address")!;
        Assert.Equal(BlazorFormFieldType.Object, address.Type);
        Assert.Equal(2, address.Children.Count);
        Assert.Equal("Town", address.Children[1].Label);
        // DataAnnotations on the child type are picked up too.
        Assert.True(address.Children[0].Required);
    }

    [Fact]
    public void Typed_array_builder_builds_an_item_template_from_the_element_type()
    {
        var form = BlazorFormBuilder.For<RegistrationModel>()
            .Array(x => x.Items, i => i
                .Field(l => l.Product)
                .Field(l => l.Quantity))
            .Build();

        var items = form.FindField("Items")!;
        Assert.Equal(BlazorFormFieldType.Array, items.Type);
        Assert.Equal(2, items.ItemTemplate!.Children.Count);
        Assert.Equal(BlazorFormFieldType.Integer, items.ItemTemplate.Children[1].Type);
    }

    [Fact]
    public void Path_exposes_a_refactor_safe_field_name_for_conditions_and_steps()
        => Assert.Equal("Address.City", BlazorFormBuilder<RegistrationModel>.Path(x => x.Address.City));

    [Fact]
    public void A_non_member_selector_is_rejected_with_guidance()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            BlazorFormBuilder.For<RegistrationModel>().Field(x => x.FirstName.Length + 1));

        Assert.Contains("property access", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Duplicate_field_names_are_rejected()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            BlazorFormBuilder.Create().Text("name").Text("Name"));

        Assert.Contains("already been added", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Form_level_rules_can_target_the_field_the_user_should_fix()
    {
        var form = BlazorFormBuilder.For<BookingModel>()
            .Field(x => x.Start)
            .Field(x => x.End)
            .MustAll(m => m.End >= m.Start, "End must be on or after start.", nameof(BookingModel.End))
            .Build();

        var model = new BookingModel { Start = new DateOnly(2024, 5, 10), End = new DateOnly(2024, 5, 1) };
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        Assert.False(await state.ValidateAsync());
        Assert.Equal("End must be on or after start.", state.MessagesFor("End")[0].Message);
    }

    [Fact]
    public void Scalar_arrays_can_be_declared_without_an_object_wrapper()
    {
        var form = BlazorFormBuilder.Create()
            .ArrayOf("tags", BlazorFormFieldType.Text, f => f.Items(max: 5))
            .Build();

        var tags = form.FindField("tags")!;
        Assert.Equal(BlazorFormFieldType.Text, tags.ItemTemplate!.Type);
        Assert.Empty(tags.ItemTemplate.Children);
    }
}

public class EnumOptionTests
{
    [Fact]
    public void Enum_options_use_Display_names_and_order()
    {
        var options = BlazorFormEnumOptions.For(typeof(Priority));

        Assert.Equal(2, options.Count); // Internal is AutoGenerateField = false
        Assert.Equal("High priority", options[0].Label);
        Assert.Equal("High", options[0].Value);
        Assert.Equal("Low priority", options[1].Label);
    }

    [Fact]
    public void Flags_enums_become_multi_selects_without_their_zero_member()
    {
        var form = BlazorFormSchemaGenerator.Generate<TypedModel>();
        var availability = form.FindField("Availability")!;

        Assert.Equal(BlazorFormFieldType.MultiSelect, availability.Type);
        Assert.Equal(3, availability.Options.Count); // None is excluded
        Assert.DoesNotContain(availability.Options, o => o.Value == "None");
    }

    [Fact]
    public void Plain_enums_stay_single_selects()
        => Assert.Equal(BlazorFormFieldType.Select, BlazorFormSchemaGenerator.Generate<TypedModel>().FindField("Priority")!.Type);
}

public class GeneratorBehaviourTests
{
    [Fact]
    public void Computed_properties_are_rendered_read_only_rather_than_editable()
    {
        var summary = BlazorFormSchemaGenerator.Generate<TypedModel>().FindField("Summary")!;
        Assert.True(summary.ReadOnly);
    }

    [Fact]
    public void Computed_properties_can_be_dropped_entirely()
    {
        var form = BlazorFormSchemaGenerator.Generate<TypedModel>(new BlazorFormSchemaGeneratorOptions
        {
            ReadOnlyProperties = BlazorFormReadOnlyPropertyHandling.Skip
        });

        Assert.Null(form.FindField("Summary"));
    }

    [Fact]
    public void Ignored_properties_are_left_out()
    {
        var options = new BlazorFormSchemaGeneratorOptions();
        options.IgnoredProperties.Add("id");

        Assert.Null(BlazorFormSchemaGenerator.Generate<TypedModel>(options).FindField("Id"));
    }

    [Fact]
    public void A_convention_hook_runs_for_every_field()
    {
        var form = BlazorFormSchemaGenerator.Generate<RegistrationModel>(new BlazorFormSchemaGeneratorOptions
        {
            ConfigureField = f => f.HelpText ??= $"Field: {f.Name}"
        });

        Assert.Equal("Field: FirstName", form.FindField("FirstName")!.HelpText);
    }

    [Fact]
    public void Well_known_reference_types_are_not_mistaken_for_object_groups()
    {
        var form = BlazorFormSchemaGenerator.Generate<TypedModel>();

        Assert.Equal(BlazorFormFieldType.Url, form.FindField("Website")!.Type);
        Assert.Equal(BlazorFormFieldType.Text, form.FindField("Id")!.Type);
        Assert.Equal(BlazorFormFieldType.Time, form.FindField("Duration")!.Type);
    }

    [Fact]
    public void Compare_annotations_become_a_cross_field_rule()
    {
        var form = BlazorFormSchemaGenerator.Generate<SignupModel>();
        Assert.Contains(form.FindField("ConfirmPassword")!.Validators, r => r.Key == "compare");
    }
}

public class RendererRegistryTests
{
    [Fact]
    public void File_fields_resolve_to_the_file_input_not_a_text_box()
    {
        var registry = new BlazorFormFieldRendererRegistry();
        var field = new BlazorFormFieldDefinition("cv", BlazorFormFieldType.File);

        Assert.Equal(typeof(BlazorFormFileInput), registry.Resolve(field));
    }

    [Fact]
    public void An_unregistered_custom_key_fails_loudly()
    {
        var registry = new BlazorFormFieldRendererRegistry();
        var field = new BlazorFormFieldDefinition("rating", BlazorFormFieldType.Custom) { CustomRenderer = "rating" };

        var ex = Assert.Throws<InvalidOperationException>(() => registry.Resolve(field));
        Assert.Contains("rating", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_renderer_key_overrides_the_type_default()
    {
        var registry = new BlazorFormFieldRendererRegistry();
        registry.RegisterCustom("fancy", typeof(BlazorFormTextAreaInput));
        var field = new BlazorFormFieldDefinition("notes", BlazorFormFieldType.Text) { CustomRenderer = "fancy" };

        Assert.Equal(typeof(BlazorFormTextAreaInput), registry.Resolve(field));
    }

    [Fact]
    public void Registering_a_non_component_is_rejected()
        => Assert.Throws<ArgumentException>(() =>
            new BlazorFormFieldRendererRegistry().Register(BlazorFormFieldType.Text, typeof(string)));
}

public class SchemaLookupTests
{
    [Fact]
    public void FindByPath_walks_objects_and_array_templates()
    {
        var form = BlazorFormSchemaGenerator.Generate<RegistrationModel>();

        Assert.Equal("City", form.FindByPath("Address.City")!.Name);
        Assert.Equal("Product", form.FindByPath("Items[0].Product")!.Name);
        Assert.Equal("Items", form.FindByPath("Items")!.Name);
        Assert.Null(form.FindByPath("Nope.Missing"));
    }
}

public class ConditionOperatorTests
{
    private static IBlazorFormDataReader Data(params (string, object?)[] values)
    {
        var d = new BlazorFormDictionaryDataAccessor();
        foreach (var (k, v) in values) d.SetValue(k, v);
        return d;
    }

    [Fact]
    public void StartsWith_and_EndsWith_compare_text()
    {
        var data = Data(("Code", "AB-1234"));
        Assert.True(new BlazorFormFieldCondition("Code", BlazorFormConditionOperator.StartsWith, "AB").Evaluate(data));
        Assert.True(new BlazorFormFieldCondition("Code", BlazorFormConditionOperator.EndsWith, "1234").Evaluate(data));
        Assert.False(new BlazorFormFieldCondition("Code", BlazorFormConditionOperator.StartsWith, "ZZ").Evaluate(data));
    }

    [Fact]
    public void Dates_compare_against_a_string_operand()
    {
        var data = Data(("Start", new DateOnly(2024, 5, 10)));
        Assert.True(new BlazorFormFieldCondition("Start", BlazorFormConditionOperator.GreaterThan, "2024-01-01").Evaluate(data));
        Assert.False(new BlazorFormFieldCondition("Start", BlazorFormConditionOperator.LessThan, "2024-01-01").Evaluate(data));
    }

    [Fact]
    public void A_malformed_regex_operand_simply_does_not_match()
    {
        var data = Data(("Code", "ABC"));
        Assert.False(new BlazorFormFieldCondition("Code", BlazorFormConditionOperator.Matches, "([unclosed").Evaluate(data));
    }

    [Fact]
    public void Numeric_equality_ignores_the_ambient_culture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            var data = Data(("Price", 1.5m));
            Assert.True(new BlazorFormFieldCondition("Price", BlazorFormConditionOperator.Equals, 1.5).Evaluate(data));
            Assert.True(new BlazorFormFieldCondition("Price", BlazorFormConditionOperator.GreaterThan, 1).Evaluate(data));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}
