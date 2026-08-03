using Microsoft.Extensions.DependencyInjection;

namespace BlazorForm.Tests;

/// <summary>
/// A collection drawn from a fixed set of answers is a set of tick boxes. A repeater is right for a
/// list of things the user writes and absurd for a list of three possible days.
/// </summary>
public class ChoiceCollectionTests
{
    private sealed class Availability
    {
        public List<AccountType> Accounts { get; set; } = [];
        public Priority[] Priorities { get; set; } = [];
        public List<AccountType?> Optional { get; set; } = [];
        public List<string> Notes { get; set; } = [];
        public List<LineItem> Lines { get; set; } = [];
    }

    private static BlazorFormDefinition Schema() => BlazorFormSchemaGenerator.Generate<Availability>();

    [Fact]
    public void A_list_of_enum_members_is_a_multi_select()
        => Assert.Equal(BlazorFormFieldType.MultiSelect, Schema().FindField("Accounts")!.Type);

    [Fact]
    public void An_array_of_enum_members_is_too()
        => Assert.Equal(BlazorFormFieldType.MultiSelect, Schema().FindField("Priorities")!.Type);

    [Fact]
    public void A_collection_of_nullable_enum_members_is_too()
        => Assert.Equal(BlazorFormFieldType.MultiSelect, Schema().FindField("Optional")!.Type);

    [Fact]
    public void The_choices_come_from_the_element_type_not_the_collection()
    {
        var options = Schema().FindField("Accounts")!.Options.Select(o => o.Value).ToList();

        Assert.Equal(Enum.GetNames<AccountType>().Length, options.Count);
        Assert.Contains("Business", options);
    }

    [Fact]
    public void A_list_of_free_text_is_still_a_repeater_because_the_answers_are_not_a_fixed_set()
        => Assert.Equal(BlazorFormFieldType.Array, Schema().FindField("Notes")!.Type);

    [Fact]
    public void A_list_of_objects_is_still_a_repeater()
        => Assert.Equal(BlazorFormFieldType.Array, Schema().FindField("Lines")!.Type);

    [Fact]
    public void The_chosen_members_reach_the_typed_model()
    {
        var model = new Availability();
        var state = new BlazorFormState(Schema(), new BlazorFormModelDataAccessor(model));

        state.SetValue("Accounts", new List<string> { "Business" });

        Assert.Equal([AccountType.Business], model.Accounts);
    }

    [Fact]
    public void Ticking_and_unticking_leaves_the_form_clean()
    {
        var model = new Availability();
        var state = new BlazorFormState(Schema(), new BlazorFormModelDataAccessor(model));

        state.SetValue("Accounts", new List<string> { "Business" });
        state.SetValue("Accounts", new List<string>());

        Assert.False(state.IsFormDirty);
    }
}

/// <summary>An array of a closed set of choices, described in JSON rather than in C#.</summary>
public class JsonChoiceCollectionTests
{
    [Fact]
    public void An_array_of_enum_values_that_may_not_repeat_imports_as_a_multi_select()
    {
        const string json = """
        {"type":"object","properties":{
          "days":{"type":"array","uniqueItems":true,
                  "items":{"type":"string","enum":["mon","tue"],"enumNames":["Monday","Tuesday"]}}
        }}
        """;

        var field = BlazorFormJsonSchemaImporter.Import(json).FindField("days")!;

        Assert.Equal(BlazorFormFieldType.MultiSelect, field.Type);
        Assert.Equal(["Monday", "Tuesday"], field.Options.Select(o => o.Label));
        // The rule has nothing left to say: a set of tick boxes cannot hold the same answer twice.
        Assert.DoesNotContain(field.Validators, v => v.Key == "uniqueItems");
    }

    [Fact]
    public void Without_uniqueItems_it_stays_a_repeater_because_the_document_allows_duplicates()
    {
        const string json = """
        {"type":"object","properties":{
          "picks":{"type":"array","items":{"type":"string","enum":["a","b"]}}
        }}
        """;

        var field = BlazorFormJsonSchemaImporter.Import(json).FindField("picks")!;

        Assert.Equal(BlazorFormFieldType.Array, field.Type);
        Assert.Equal(BlazorFormFieldType.Select, field.ItemTemplate!.Type);
    }

    [Fact]
    public void An_array_of_objects_is_untouched_by_the_promotion()
    {
        const string json = """
        {"type":"object","properties":{
          "lines":{"type":"array","uniqueItems":true,
                   "items":{"type":"object","properties":{"product":{"type":"string"}}}}
        }}
        """;

        var field = BlazorFormJsonSchemaImporter.Import(json).FindField("lines")!;

        Assert.Equal(BlazorFormFieldType.Array, field.Type);
        Assert.Equal(BlazorFormFieldType.Object, field.ItemTemplate!.Type);
    }

    [Fact]
    public void A_multi_select_keeps_its_size_bounds_across_a_round_trip()
    {
        var form = BlazorFormBuilder.Create()
            .MultiSelect("days", f => f
                .Options(("mon", "Monday"), ("tue", "Tuesday"), ("wed", "Wednesday"))
                .Items(min: 1, max: 2))
            .Build();

        var field = BlazorFormJsonSchemaImporter
            .Import(BlazorFormJsonSchemaExporter.Export(form))
            .FindField("days")!;

        Assert.Equal(BlazorFormFieldType.MultiSelect, field.Type);
        Assert.Equal(1, field.MinItems);
        Assert.Equal(2, field.MaxItems);
        Assert.Contains(field.Validators, v => v.Key == "items");
    }

    [Fact]
    public void A_repeater_of_choices_this_library_exported_comes_back_as_a_repeater()
    {
        // Same shape the promotion looks for — an array of a closed set that may not repeat — but the
        // author already said what they wanted, and an export says so explicitly.
        var form = BlazorFormBuilder.Create()
            .ArrayOf("picks", BlazorFormFieldType.Select,
                f => f.UniqueItems(),
                item => item.Options(("a", "A"), ("b", "B")))
            .Build();

        var field = BlazorFormJsonSchemaImporter
            .Import(BlazorFormJsonSchemaExporter.Export(form))
            .FindField("picks")!;

        Assert.Equal(BlazorFormFieldType.Array, field.Type);
        Assert.NotNull(field.ItemTemplate);
        Assert.Contains(field.Validators, v => v.Key == "uniqueItems");
    }

    [Fact]
    public void A_tag_list_keeps_its_size_bounds_too()
    {
        var form = BlazorFormBuilder.Create().Tags("skills", f => f.AsTags(max: 5)).Build();

        var field = BlazorFormJsonSchemaImporter
            .Import(BlazorFormJsonSchemaExporter.Export(form))
            .FindField("skills")!;

        Assert.Equal(BlazorFormFieldType.Tags, field.Type);
        Assert.Equal(5, field.MaxItems);
    }
}

/// <summary>
/// Registering the renderer registry twice must configure the one that is there, not build a second
/// one and quietly drop it.
/// </summary>
public class ServiceRegistrationTests
{
    private sealed class Dummy : BlazorFormInputBase { }

    [Fact]
    public void A_later_call_configures_the_registry_the_first_one_registered()
    {
        var services = new ServiceCollection();

        // What a component library or a shared module does…
        services.AddBlazorForm();
        // …and then what the application does.
        services.AddBlazorForm(r => r.RegisterCustom<Dummy>("rating"));

        var registry = services.BuildServiceProvider().GetRequiredService<IBlazorFormFieldRendererRegistry>();
        var field = new BlazorFormFieldDefinition("score", BlazorFormFieldType.Custom) { CustomRenderer = "rating" };

        Assert.True(registry.TryResolve(field, out var component));
        Assert.Equal(typeof(Dummy), component);
    }

    [Fact]
    public void Both_calls_contribute_rather_than_the_last_one_winning()
    {
        var services = new ServiceCollection();
        services.AddBlazorForm(r => r.RegisterCustom<Dummy>("first"));
        services.AddBlazorForm(r => r.RegisterCustom<Dummy>("second"));

        var registry = services.BuildServiceProvider().GetRequiredService<IBlazorFormFieldRendererRegistry>();

        Assert.True(registry.TryResolve(
            new BlazorFormFieldDefinition("a", BlazorFormFieldType.Custom) { CustomRenderer = "first" }, out _));
        Assert.True(registry.TryResolve(
            new BlazorFormFieldDefinition("b", BlazorFormFieldType.Custom) { CustomRenderer = "second" }, out _));
    }

    [Fact]
    public void Only_one_registry_is_ever_registered()
    {
        var services = new ServiceCollection();
        services.AddBlazorForm();
        services.AddBlazorForm();

        Assert.Single(services, d => d.ServiceType == typeof(IBlazorFormFieldRendererRegistry));
    }
}
