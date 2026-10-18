using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using Bunit;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;

namespace BlazorForm.Tests;

// ---------------------------------------------------------------- models

public struct DvPoint
{
    public int X { get; set; }
    public int Y { get; set; }
}

public struct DvFrame
{
    public DvPoint Origin { get; set; }
}

public class DvShapes
{
    public DvPoint Point { get; set; }
    public DvPoint? Maybe { get; set; }
    public DvFrame Frame { get; set; }
    public List<DvPoint> Points { get; set; } = [];
}

public class DvAccess
{
    public string Id { get; private set; } = "orig";
    public bool IsAdmin { get; internal set; }
    public string Code { get; init; } = "";
    public string Secret { private get; set; } = "hidden";
    public string Name { get; set; } = "";
}

public class DvAddress { public string? City { get; set; } }
public class DvCustomer { public DvAddress Address { get; set; } = new(); }
public class DvLine { public string? Product { get; set; } }
public class DvOrder { public List<DvLine> Lines { get; set; } = []; }
public class DvOuter
{
    public DvCustomer Customer { get; set; } = new();
    public DvOrder Order { get; set; } = new();
}

public class DvRow
{
    public string? Name { get; set; }
    public int Qty { get; set; }
}

public record DvTag(string Name, List<int> Codes);

public class DvLoop
{
    public string Name { get; set; } = "";
    public DvLoop? Self { get; set; }
}

[Flags]
public enum DvPerm { None = 0, Read = 1, Write = 2 }

public class DvFlagsModel { public DvPerm Perms { get; set; } }

public class DvAnnotated
{
    [Range(0d, 1d, MinimumIsExclusive = true, MaximumIsExclusive = true)]
    public double Ratio { get; set; }

    [MinLength(1)]
    public List<DayOfWeek> Days { get; set; } = [];

    [MaxLength(2)]
    public List<IBrowserFile> Attachments { get; set; } = [];

    [Url]
    public string? Site { get; set; }

    [RegularExpression(@"\d{5}")]
    public string? Zip { get; set; }

    [RegularExpression("a|b")]
    public string? Letter { get; set; }

    [FileExtensions(Extensions = "pdf,docx")]
    public string? FileName { get; set; }

    [FileExtensions(Extensions = "png")]
    public IBrowserFile? Picture { get; set; }

    [DataType(DataType.Upload)]
    public string? UploadName { get; set; }

    [Range(typeof(DateTime), "0050-01-01", "2030-01-01")]
    public DateTime Ancient { get; set; }
}

public class DvCultureBounds
{
    [Range(typeof(decimal), "0,01", "99,99")]
    public decimal Price { get; set; }

    [Range(typeof(decimal), "0.01", "99.99", ParseLimitsInInvariantCulture = true)]
    public decimal Invariant { get; set; }
}

public class DvBinary
{
    public string Name { get; set; } = "";
    public byte[]? Thumbnail { get; set; }
    public Stream? Archive { get; set; }
    public List<byte[]>? Pages { get; set; }
}

public class DvCollections
{
    public HashSet<DayOfWeek> Days { get; set; } = [];
    public ISet<string> Labels { get; set; } = new HashSet<string>();
    public ObservableCollection<string> Watched { get; set; } = [];
    public ReadOnlyCollection<string> Frozen { get; set; } = new([]);
}

public class DvNullable { public int? Age { get; set; } }

public class DvFieldModel
{
#pragma warning disable CA1051 // a public field is exactly what this test needs
    public string? PublicField;
#pragma warning restore CA1051
}

public class DvValidatable : IValidatableObject
{
    public int Calls { get; private set; }

    [Required]
    public string? Name { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        Calls++;
        yield return new ValidationResult("Whole-model problem.");
    }
}

public class DvDates { public DateOnly? When { get; set; } }

public class DvStamped { public DateTimeOffset When { get; set; } }

public class DvParent
{
    public int NavigationReads { get; set; }
    public List<DvChild> Children { get; } = [];
}

public class DvChild(DvParent parent)
{
    public string? Name { get; set; }
    public int N { get; set; }

    // A navigation property: on an entity, reading it would lazy-load the parent row.
    public DvParent Parent
    {
        get
        {
            parent.NavigationReads++;
            return parent;
        }
    }
}

public class DvAggregate
{
    public DvAddress Shipping { get; private set; } = new();
    public List<DvLine> Lines { get; private set; } = [];
    public List<DvLine> GetOnlyLines { get; } = [];
    public DvPoint Spot { get; private set; }
    public string[] Codes { get; private set; } = [];
    public string Reference { get; private set; } = "";
}

public class DvAggregateView
{
    public DvAddress Shipping { get; private set; } = new();
    public List<DvLine> Lines { get; private set; } = [new DvLine()];
}

[CustomValidation(typeof(DvRanged), nameof(Check))]
public class DvRanged
{
    [Required]
    public string? Start { get; set; }

    // Written, as class-level checks are, on the assumption that [Required] has already passed.
    public static ValidationResult? Check(DvRanged model, ValidationContext context)
        => model.Start!.Length > 1 ? ValidationResult.Success : new ValidationResult("Start is too short.");
}

public sealed class DvFile(string name) : IBrowserFile
{
    public string Name => name;
    public DateTimeOffset LastModified => default;
    public long Size => 10;
    public string ContentType => "application/octet-stream";
    public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) => Stream.Null;
}

// ---------------------------------------------------------------- tests

public class DataValidationRegressionTests
{
    private static BlazorFormRuleResult Run(IBlazorFormValidationRule rule, object? value,
        BlazorFormFieldDefinition? field = null, IBlazorFormDataReader? data = null)
        => rule.ValidateAsync(new BlazorFormValidationContext("x", value, data ?? new BlazorFormDictionaryDataAccessor(), null, field))
            .AsTask().GetAwaiter().GetResult();

    private static bool Valid(BlazorFormFieldDefinition field, object? value)
        => field.Validators.All(rule => Run(rule, value, field).IsValid);

    private static T InCulture<T>(string culture, Func<T> body)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(culture);
        try { return body(); }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    // ---- #1: a date before the year 100 no longer takes validation down

    [Fact]
    public void A_date_before_the_year_100_fails_a_minimum_instead_of_throwing()
    {
        var rule = new BlazorFormRangeRule(new DateTime(2020, 1, 1).ToOADate(), new DateTime(2030, 1, 1).ToOADate());

        Assert.False(Run(rule, new DateOnly(50, 1, 1)).IsValid);
        Assert.False(Run(rule, new DateTime(2, 5, 1)).IsValid);
        Assert.False(Run(rule, "0099-05-01").IsValid);
    }

    [Fact]
    public void A_date_before_the_year_100_is_below_a_maximum()
    {
        var rule = new BlazorFormRangeRule(null, new DateTime(2030, 1, 1).ToOADate());

        Assert.True(Run(rule, new DateOnly(50, 1, 1)).IsValid);
    }

    [Fact]
    public async Task A_form_given_a_year_99_date_reports_it_rather_than_throwing()
    {
        var form = BlazorFormBuilder.For<DvDates>()
            .Field(x => x.When, f => f.Range(new DateOnly(2020, 1, 1), new DateOnly(2030, 1, 1)))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(new DvDates()));
        state.SetValue("When", "0099-05-01");

        Assert.False(await state.ValidateAsync());
        Assert.NotEmpty(state.MessagesFor("When"));
    }

    [Fact]
    public void A_range_bound_before_the_year_100_is_no_bound_rather_than_an_exception()
    {
        var field = BlazorFormSchemaGenerator.Generate<DvAnnotated>().FindField("Ancient")!;

        Assert.Null(field.Min);
        Assert.NotNull(field.Max);
    }

    // ---- #2: writes through a struct reach the model

    [Fact]
    public void A_write_through_a_struct_property_reaches_the_model()
    {
        var model = new DvShapes();
        var data = new BlazorFormModelDataAccessor(model);

        data.SetValue("Point.X", 5);

        Assert.False(data.LastWriteFailed);
        Assert.Equal(5, model.Point.X);
    }

    [Fact]
    public void A_write_through_a_struct_inside_a_struct_reaches_the_model()
    {
        var model = new DvShapes();
        var data = new BlazorFormModelDataAccessor(model);

        data.SetValue("Frame.Origin.Y", 3);

        Assert.Equal(3, model.Frame.Origin.Y);
    }

    [Fact]
    public void A_write_into_a_struct_list_element_reaches_the_list()
    {
        var model = new DvShapes { Points = [new DvPoint(), new DvPoint()] };
        var data = new BlazorFormModelDataAccessor(model);

        data.SetValue("Points[1].X", 7);

        Assert.Equal(7, model.Points[1].X);
        Assert.Equal(0, model.Points[0].X);
    }

    [Fact]
    public void A_write_through_a_nullable_struct_creates_and_keeps_it()
    {
        var model = new DvShapes();
        var data = new BlazorFormModelDataAccessor(model);

        data.SetValue("Maybe.X", 9);
        data.SetValue("Maybe.Y", 4);

        Assert.Equal(9, model.Maybe?.X);
        Assert.Equal(4, model.Maybe?.Y);
    }

    [Fact]
    public void A_generated_struct_group_is_editable_through_the_form()
    {
        var model = new DvShapes();
        var state = new BlazorFormState(BlazorFormSchemaGenerator.Generate<DvShapes>(), new BlazorFormModelDataAccessor(model));

        state.SetValue("Point.X", 42);

        Assert.Equal(42, model.Point.X);
        Assert.Equal(42, state.GetValue("Point.X"));
    }

    // ---- #3: uniqueItems compares content

    [Fact]
    public void Distinct_dictionary_rows_are_not_duplicates()
    {
        var rows = new List<object?>
        {
            new Dictionary<string, object?> { ["name"] = "A" },
            new Dictionary<string, object?> { ["name"] = "B" }
        };

        Assert.True(Run(new BlazorFormUniqueItemsRule(), rows).IsValid);
    }

    [Fact]
    public void Equal_dictionary_rows_are_duplicates_whatever_the_key_casing_or_order()
    {
        var rows = new List<object?>
        {
            new Dictionary<string, object?> { ["name"] = "A", ["qty"] = 1L },
            new Dictionary<string, object?> { ["QTY"] = 1.0, ["Name"] = "A" }
        };

        Assert.False(Run(new BlazorFormUniqueItemsRule(), rows).IsValid);
    }

    [Fact]
    public void Model_rows_are_compared_by_their_properties()
    {
        var rule = new BlazorFormUniqueItemsRule();

        Assert.True(Run(rule, new List<DvRow> { new() { Name = "A" }, new() { Name = "B" } }).IsValid);
        Assert.False(Run(rule, new List<DvRow> { new() { Name = "A", Qty = 2 }, new() { Name = "A", Qty = 2 } }).IsValid);
    }

    [Fact]
    public void Model_rows_are_compared_by_their_scalar_members_only()
    {
        // A model row's reference-typed members (a list here, a navigation property on an entity) are
        // not read at all, so two rows that differ only there are the same row as far as the rule goes.
        var rule = new BlazorFormUniqueItemsRule();

        Assert.False(Run(rule, new List<DvTag> { new("a", [1, 2]), new("a", [2, 1]) }).IsValid);
        Assert.True(Run(rule, new List<DvTag> { new("a", [1, 2]), new("b", [1, 2]) }).IsValid);
    }

    [Fact]
    public async Task A_model_rows_navigation_members_are_never_read()
    {
        var parent = new DvParent();
        for (var i = 0; i < 2000; i++) parent.Children.Add(new DvChild(parent) { Name = "c" + i, N = i });

        var ctx = new BlazorFormValidationContext("Children", parent.Children, new BlazorFormModelDataAccessor(parent));

        Assert.True((await new BlazorFormUniqueItemsRule().ValidateAsync(ctx)).IsValid);
        Assert.Equal(0, parent.NavigationReads);
    }

    [Fact]
    public void A_dictionary_row_that_refers_back_to_the_form_is_cut_off_there()
    {
        var root = new Dictionary<string, object?>();
        var rows = new List<object?>
        {
            new Dictionary<string, object?> { ["name"] = "A", ["owner"] = root },
            new Dictionary<string, object?> { ["name"] = "B", ["owner"] = root }
        };
        root["rows"] = rows;
        var data = new BlazorFormDictionaryDataAccessor(root);

        Assert.True(Run(new BlazorFormUniqueItemsRule(), rows, data: data).IsValid);
        rows.Add(new Dictionary<string, object?> { ["name"] = "A", ["owner"] = root });
        Assert.False(Run(new BlazorFormUniqueItemsRule(), rows, data: data).IsValid);
    }

    [Fact]
    public void Numbers_are_equal_by_value_whatever_type_holds_them()
    {
        var rule = new BlazorFormUniqueItemsRule();

        Assert.False(Run(rule, new List<object?> { 1, 1.0m }).IsValid);
        Assert.False(Run(rule, new List<object?> { 1L, 1.0 }).IsValid);
        Assert.False(Run(rule, new List<object?> { 0.1, 0.1m }).IsValid);
        Assert.True(Run(rule, new List<object?> { 1, 2 }).IsValid);
    }

    [Fact]
    public void Text_and_numbers_are_never_equal_and_text_is_compared_exactly()
    {
        var rule = new BlazorFormUniqueItemsRule();

        Assert.True(Run(rule, new List<object?> { "1", 1 }).IsValid);
        Assert.True(Run(rule, new List<object?> { "a", "A" }).IsValid);
        Assert.True(Run(rule, new List<object?> { new List<object?> { "a,b" }, new List<object?> { "a", "b" } }).IsValid);
    }

    [Fact]
    public void Json_items_are_compared_as_the_values_they_stand_for()
    {
        var rule = new BlazorFormUniqueItemsRule();
        var same = JsonSerializer.Deserialize<List<object?>>("""["a", "a"]""");
        var different = JsonSerializer.Deserialize<List<object?>>("""["a", "b"]""");
        var numbers = JsonSerializer.Deserialize<List<object?>>("""[1, 1.0]""");

        Assert.False(Run(rule, same).IsValid);
        Assert.True(Run(rule, different).IsValid);
        Assert.False(Run(rule, numbers).IsValid);
    }

    [Fact]
    public void An_object_graph_that_loops_does_not_hang_the_rule()
    {
        var a = new DvLoop { Name = "a" };
        a.Self = a;
        var b = new DvLoop { Name = "b" };
        b.Self = b;

        Assert.True(Run(new BlazorFormUniqueItemsRule(), new List<DvLoop> { a, b }).IsValid);
    }

    [Fact]
    public async Task A_json_schema_array_of_objects_with_uniqueItems_accepts_two_different_rows()
    {
        const string json = """{"type":"object","properties":{"rows":{"type":"array","uniqueItems":true,"items":{"type":"object","properties":{"name":{"type":"string"}}}}}}""";
        var data = new BlazorFormDictionaryDataAccessor();
        data.SetValue("rows[0].name", "A");
        data.SetValue("rows[1].name", "B");
        var state = new BlazorFormState(BlazorFormJsonSchemaImporter.Import(json), data);

        Assert.True(await state.ValidateAsync());

        state.SetValue("rows[1].name", "A");
        Assert.False(await state.ValidateAsync());
    }

    // ---- #4: [RegularExpression] keeps its whole-value meaning

    [Fact]
    public void RegularExpression_must_match_the_whole_value()
    {
        var field = BlazorFormSchemaGenerator.Generate<DvAnnotated>().FindField("Zip")!;

        Assert.Equal(@"^(?:\d{5})$", field.Pattern);
        Assert.True(Valid(field, "12345"));
        Assert.False(Valid(field, "abc12345xyz"));
        Assert.Equal(new RegularExpressionAttribute(@"\d{5}").IsValid("abc12345xyz"), Valid(field, "abc12345xyz"));
    }

    [Fact]
    public void RegularExpression_does_not_accept_a_trailing_newline()
    {
        // .NET's `$` also matches before a final newline; the rule ends at \z, as the attribute's own
        // whole-length check does. The rendered pattern keeps `$`, the browser's dialect.
        var field = BlazorFormSchemaGenerator.Generate<DvAnnotated>().FindField("Zip")!;

        Assert.False(new RegularExpressionAttribute(@"\d{5}").IsValid("12345\n"));
        Assert.False(Valid(field, "12345\n"));
        Assert.Equal(@"^(?:\d{5})\z", field.Validators.OfType<BlazorFormPatternRule>().Single().Pattern);
    }

    [Fact]
    public void RegularExpression_with_an_alternation_is_anchored_at_both_ends()
    {
        var field = BlazorFormSchemaGenerator.Generate<DvAnnotated>().FindField("Letter")!;

        Assert.True(Valid(field, "a"));
        Assert.False(Valid(field, "ab"));
        Assert.False(Valid(field, "xb"));
    }

    // ---- #5: only public accessors are the form's to use

    [Fact]
    public void A_property_with_a_non_public_setter_is_generated_read_only()
    {
        var form = BlazorFormSchemaGenerator.Generate<DvAccess>();

        Assert.True(form.FindField("Id")!.ReadOnly);
        Assert.True(form.FindField("IsAdmin")!.ReadOnly);
        Assert.False(form.FindField("Code")!.ReadOnly); // init is a public setter
        Assert.False(form.FindField("Name")!.ReadOnly);
    }

    [Fact]
    public void A_property_with_a_non_public_getter_is_not_generated_or_read()
    {
        var form = BlazorFormSchemaGenerator.Generate<DvAccess>();
        var data = new BlazorFormModelDataAccessor(new DvAccess());

        Assert.Null(form.FindField("Secret"));
        Assert.Null(data.GetValue("Secret"));
        Assert.False(data.TryGetValue("Secret", out _));
    }

    [Fact]
    public void A_property_with_a_non_public_setter_is_skipped_when_read_only_properties_are()
    {
        var form = BlazorFormSchemaGenerator.Generate<DvAccess>(new BlazorFormSchemaGeneratorOptions
        {
            ReadOnlyProperties = BlazorFormReadOnlyPropertyHandling.Skip
        });

        Assert.Null(form.FindField("Id"));
        Assert.Null(form.FindField("IsAdmin"));
        Assert.NotNull(form.FindField("Name"));
    }

    [Fact]
    public void The_accessor_refuses_to_write_through_a_non_public_setter()
    {
        var model = new DvAccess();
        var data = new BlazorFormModelDataAccessor(model);

        data.SetValue("Id", "hacked");
        Assert.True(data.LastWriteFailed);
        Assert.Equal("orig", model.Id);

        data.SetValue("IsAdmin", true);
        Assert.True(data.LastWriteFailed);
        Assert.False(model.IsAdmin);
    }

    [Fact]
    public void The_typed_builder_marks_a_property_without_a_public_setter_read_only()
    {
        var form = BlazorFormBuilder.For<DvAccess>().Field(x => x.Id).Field(x => x.Name).Build();

        Assert.True(form.FindField("Id")!.ReadOnly);
        Assert.False(form.FindField("Name")!.ReadOnly);
    }

    [Fact]
    public void A_class_group_or_list_with_a_non_public_setter_stays_editable()
    {
        // Children are written on the object the property already holds and rows are added to the list
        // in place, so the setter is never needed — and a read-only container would lock everything in it.
        var form = BlazorFormSchemaGenerator.Generate<DvAggregate>();

        Assert.False(form.FindField("Shipping")!.ReadOnly);
        Assert.False(form.FindField("Lines")!.ReadOnly);
        Assert.False(form.FindField("GetOnlyLines")!.ReadOnly);
        Assert.True(form.FindField("Spot")!.ReadOnly);   // a struct is written back through its setter
        Assert.True(form.FindField("Codes")!.ReadOnly);  // an array grows by being replaced
        Assert.True(form.FindField("Reference")!.ReadOnly);

        var built = BlazorFormBuilder.For<DvAggregate>().Field(x => x.Lines).Field(x => x.Reference).Build();
        Assert.False(built.FindField("Lines")!.ReadOnly);
        Assert.True(built.FindField("Reference")!.ReadOnly);
    }

    [Fact]
    public void A_class_group_or_list_with_a_non_public_setter_is_edited_in_place()
    {
        var model = new DvAggregate();
        var form = BlazorFormSchemaGenerator.Generate<DvAggregate>();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        state.SetValue("Shipping.City", "Delft");
        state.AddArrayItem(form.FindField("Lines")!, "Lines");
        state.SetValue("Lines[0].Product", "Widget");

        Assert.Equal("Delft", model.Shipping.City);
        Assert.Equal("Widget", Assert.Single(model.Lines).Product);
    }

    // ---- #2 follow-up: raw content through the typed builder fails where it is written

    [Fact]
    public void The_typed_builder_refuses_raw_content_without_a_renderer()
    {
        var ex = Assert.Throws<ArgumentException>(() => BlazorFormBuilder.For<DvBinary>().Field(x => x.Thumbnail));
        Assert.Contains("CustomRenderer", ex.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => BlazorFormBuilder.For<DvBinary>().Field(x => x.Archive));

        var form = BlazorFormBuilder.For<DvBinary>().Field(x => x.Thumbnail, f => f.CustomRenderer("thumbnail")).Build();
        Assert.Equal("thumbnail", form.Fields[0].CustomRenderer);
    }

    // ---- condition evaluation stays exception-free on text that is not the typed value

    [Fact]
    public void Comparing_typed_values_with_unparseable_text_throws_nothing()
    {
        var data = new BlazorFormDictionaryDataAccessor(new Dictionary<string, object?>
        {
            ["Code"] = "other",
            ["Count"] = 3L,
            ["When"] = new DateTime(2024, 1, 1),
            ["Id"] = Guid.NewGuid(),
            ["Day"] = DayOfWeek.Monday
        });
        BlazorFormFieldCondition[] conditions =
        [
            new("Code", BlazorFormConditionOperator.Equals, 1),
            new("Count", BlazorFormConditionOperator.Equals, ""),
            new("Count", BlazorFormConditionOperator.GreaterThan, "lots"),
            new("When", BlazorFormConditionOperator.Equals, "free text"),
            new("When", BlazorFormConditionOperator.Equals, 5),
            new("Id", BlazorFormConditionOperator.Equals, "not a guid"),
            new("Day", BlazorFormConditionOperator.Equals, "Someday"),
            new("Day", BlazorFormConditionOperator.Equals, "   ")
        ];

        // The handler runs on the throwing thread; counting only this one keeps other tests out of it.
        var thread = Environment.CurrentManagedThreadId;
        var thrown = 0;
        void Count(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            if (Environment.CurrentManagedThreadId == thread) thrown++;
        }

        AppDomain.CurrentDomain.FirstChanceException += Count;
        try
        {
            foreach (var condition in conditions)
                Assert.False(condition.Evaluate(data));
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= Count;
        }

        Assert.Equal(0, thrown);
    }

    // ---- #6: paths beneath a container whose name is dotted

    private static BlazorFormDefinition DottedContainers() => BlazorFormBuilder.For<DvOuter>()
        .Object(x => x.Customer.Address, a => a.Field(p => p.City, f => f.Required()))
        .Array(x => x.Order.Lines, l => l.Field(p => p.Product))
        .Build();

    [Fact]
    public void FindByPath_reaches_fields_beneath_a_dotted_container()
    {
        var form = DottedContainers();

        Assert.Equal("City", form.FindByPath("Customer.Address.City")?.Name);
        Assert.Equal("Product", form.FindByPath("Order.Lines[0].Product")?.Name);
        Assert.Equal("item", form.FindByPath("Order.Lines[3]")?.Name);
        Assert.Null(form.FindByPath("Customer.Address.Nope"));
        Assert.Null(form.FindByPath("Customer.Nope"));
    }

    [Fact]
    public void FindByPath_still_resolves_ordinary_paths()
    {
        var form = BlazorFormBuilder.Create()
            .Object("Address", a => a.Text("City"))
            .Array("Items", i => i.Text("Product"))
            .Build();

        Assert.Equal("City", form.FindByPath("address.city")?.Name);
        Assert.Equal("Product", form.FindByPath("Items[2].Product")?.Name);
        Assert.Null(form.FindByPath("[0]"));
    }

    [Fact]
    public void A_change_handler_beneath_a_dotted_container_runs()
    {
        var hits = 0;
        var form = BlazorFormBuilder.For<DvOuter>()
            .Object(x => x.Customer.Address, a => a.Field(p => p.City, f => f.OnChange(_ => hits++)))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(new DvOuter()));

        state.SetValue("Customer.Address.City", "Paris");

        Assert.Equal(1, hits);
    }

    [Fact]
    public async Task A_wizard_step_validates_a_field_beneath_a_dotted_container()
    {
        var form = BlazorFormBuilder.For<DvOuter>()
            .Object(x => x.Customer.Address, a => a.Field(p => p.City, f => f.Required()))
            .Step("where", s => s.Fields("Customer.Address.City"))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(new DvOuter()));

        Assert.False(await state.ValidateStepAsync());
        Assert.True(await state.ValidateFieldAsync("Customer.Address.City"));
        Assert.DoesNotContain(form.Validate(), d => d.Message.Contains("not in the schema", StringComparison.Ordinal));
    }

    // ---- #7: multipleOf on realistic amounts

    [Theory]
    [InlineData(1234567.89, true)]
    [InlineData(99999999.99, true)]
    [InlineData(0.07, true)]
    [InlineData(4.35, true)]
    [InlineData(1234567.891, false)]
    [InlineData(0.005, false)]
    public void MultipleOf_a_cent_judges_large_amounts_correctly(double value, bool expected)
        => Assert.Equal(expected, Run(new BlazorFormMultipleOfRule(0.01), value).IsValid);

    [Fact]
    public void MultipleOf_still_handles_binary_fractions_decimals_and_non_numbers()
    {
        Assert.True(Run(new BlazorFormMultipleOfRule(0.1), 0.3).IsValid);
        Assert.False(Run(new BlazorFormMultipleOfRule(0.1), 0.35).IsValid);
        Assert.True(Run(new BlazorFormMultipleOfRule(0.01), 123456789.12m).IsValid);
        Assert.False(Run(new BlazorFormMultipleOfRule(5), double.NaN).IsValid);
        Assert.True(Run(new BlazorFormMultipleOfRule(5), 15L).IsValid);
    }

    // ---- #9: the dictionary store

    [Fact]
    public void The_callers_dictionary_is_the_live_store_at_every_level()
    {
        var address = new Dictionary<string, object?> { ["city"] = "Paris" };
        var data = new Dictionary<string, object?> { ["name"] = "Ann", ["address"] = address };
        var store = new BlazorFormDictionaryDataAccessor(data);

        store.SetValue("name", "Bea");
        store.SetValue("address.city", "Rome");
        store.SetValue("added", 1);

        Assert.Same(data, store.Root);
        Assert.Equal("Bea", data["name"]);
        Assert.Equal("Rome", address["city"]);
        Assert.Equal(1, data["added"]);
    }

    [Fact]
    public void A_case_sensitive_nested_dictionary_is_read_and_written_without_regard_to_case()
    {
        var address = new Dictionary<string, object?> { ["City"] = "Paris" };
        var store = new BlazorFormDictionaryDataAccessor(new Dictionary<string, object?> { ["Address"] = address });

        Assert.Equal("Paris", store.GetValue("address.city"));

        store.SetValue("ADDRESS.city", "Rome");

        Assert.Equal(["City"], address.Keys);
        Assert.Equal("Rome", address["City"]);
    }

    [Fact]
    public void Writing_past_the_end_of_an_array_grows_it_and_keeps_its_type()
    {
        var data = new Dictionary<string, object?> { ["tags"] = new[] { "a" } };
        var store = new BlazorFormDictionaryDataAccessor(data);

        store.SetValue("tags[2]", "c");

        Assert.False(store.LastWriteFailed);
        var tags = Assert.IsType<string[]>(data["tags"]);
        Assert.Equal(3, tags.Length);
        Assert.Equal("a", tags[0]);
        Assert.Null(tags[1]);
        Assert.Equal("c", tags[2]);
    }

    [Fact]
    public void A_typed_list_is_padded_with_its_own_default_and_given_converted_values()
    {
        var numbers = new List<int> { 1 };
        var store = new BlazorFormDictionaryDataAccessor(new Dictionary<string, object?> { ["nums"] = numbers });

        store.SetValue("nums[3]", "7");

        Assert.False(store.LastWriteFailed);
        Assert.Equal([1, 0, 0, 7], numbers);
    }

    [Fact]
    public void A_value_a_typed_container_cannot_hold_is_reported_not_thrown()
    {
        var names = new List<string> { "a" };
        var scores = new Dictionary<string, int> { ["math"] = 9 };
        var store = new BlazorFormDictionaryDataAccessor(new Dictionary<string, object?>
        {
            ["names"] = names,
            ["scores"] = scores
        });

        store.SetValue("names[0].first", "x");
        Assert.True(store.LastWriteFailed);
        Assert.Equal(["a"], names);

        store.SetValue("scores.math", "abc");
        Assert.True(store.LastWriteFailed);
        Assert.Equal(9, scores["math"]);

        store.SetValue("bad[", 1);
        Assert.True(store.LastWriteFailed);
    }

    [Fact]
    public void A_read_only_seed_dictionary_is_copied_rather_than_locking_the_form()
    {
        var seed = new Dictionary<string, object?> { ["a"] = 1 };
        var readOnly = new ReadOnlyDictionary<string, object?>(seed);
        var store = new BlazorFormDictionaryDataAccessor(readOnly);

        store.SetValue("a", 2);
        store.SetValue("b", 3);

        Assert.False(store.LastWriteFailed);
        Assert.Equal(2, store.GetValue("A"));
        Assert.Equal(3, store.GetValue("b"));
        Assert.Equal(1, seed["a"]);
        Assert.NotSame(readOnly, store.Root);

        var immutable = new BlazorFormDictionaryDataAccessor(
            System.Collections.Immutable.ImmutableDictionary<string, object?>.Empty.Add("x", "y"));
        immutable.SetValue("x", "z");
        Assert.False(immutable.LastWriteFailed);
        Assert.Equal("z", immutable.GetValue("x"));
    }

    [Fact]
    public void A_read_only_nested_container_is_replaced_by_a_writable_copy()
    {
        var frozen = new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?> { ["keep"] = 1 });
        var data = new Dictionary<string, object?> { ["frozen"] = frozen, ["list"] = new ReadOnlyCollection<object?>(["a"]) };
        var store = new BlazorFormDictionaryDataAccessor(data);

        store.SetValue("frozen.key", 2);
        store.SetValue("list[1]", "b");

        Assert.False(store.LastWriteFailed);
        Assert.Equal(1, store.GetValue("frozen.keep"));
        Assert.Equal(2, store.GetValue("frozen.key"));
        Assert.Equal("b", store.GetValue("list[1]"));
        Assert.NotSame(frozen, data["frozen"]);
    }

    [Fact]
    public void A_typed_nested_dictionary_is_written_into_not_replaced()
    {
        var scores = new Dictionary<string, int> { ["math"] = 9, ["art"] = 7 };
        var data = new Dictionary<string, object?> { ["scores"] = scores };
        var store = new BlazorFormDictionaryDataAccessor(data);

        store.SetValue("scores.math", 5L);

        Assert.False(store.LastWriteFailed);
        Assert.Same(scores, data["scores"]);
        Assert.Equal(5, scores["math"]);
        Assert.Equal(7, store.GetValue("scores.art"));
    }

    [Fact]
    public void Deserialized_json_is_read_and_written_through_and_becomes_clr_values()
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(
            """{"name":"Ann","age":30,"ratio":0.5,"tags":["x","y"],"address":{"city":"Paris","zip":"75001"}}""")!;
        var store = new BlazorFormDictionaryDataAccessor(data);

        Assert.Equal("Paris", store.GetValue("address.city"));
        Assert.Equal(30L, store.GetValue("age"));
        Assert.Equal(0.5, store.GetValue("ratio"));
        Assert.Equal("y", store.GetValue("tags[1]"));

        store.SetValue("address.city", "Rome");

        Assert.Equal("75001", store.GetValue("address.zip"));
        var address = Assert.IsAssignableFrom<IDictionary<string, object?>>(data["address"]);
        Assert.Equal("Rome", address["city"]);
        Assert.IsType<long>(data["age"]);
    }

    [Fact]
    public async Task Rules_see_clr_values_in_a_form_over_deserialized_json()
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, object?>>("""{"age":30}""")!;
        var form = BlazorFormBuilder.Create().Integer("age", f => f.Range(0, 10)).Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor(data));

        Assert.False(await state.ValidateAsync());
        Assert.NotEmpty(state.MessagesFor("age"));
    }

    // ---- #10: any constructible collection is a valid target

    [Fact]
    public void A_multi_select_writes_into_sets_and_other_collections()
    {
        var model = new DvCollections();
        var data = new BlazorFormModelDataAccessor(model);

        data.SetValue("Days", new List<string> { "Monday", "Tuesday" });
        Assert.False(data.LastWriteFailed);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Tuesday], model.Days.OrderBy(d => d));

        data.SetValue("Labels", new List<string> { "a", "b" });
        Assert.False(data.LastWriteFailed);
        Assert.True(model.Labels.SetEquals(["a", "b"]));

        data.SetValue("Watched", new List<string> { "w" });
        Assert.False(data.LastWriteFailed);
        Assert.Equal(["w"], model.Watched);

        data.SetValue("Frozen", new List<string> { "f" });
        Assert.False(data.LastWriteFailed);
        Assert.Equal(["f"], model.Frozen);
    }

    // ---- #11: raw content and file names

    [Fact]
    public void Byte_arrays_streams_and_lists_of_them_are_not_generated()
    {
        var form = BlazorFormSchemaGenerator.Generate<DvBinary>();

        Assert.Equal(["Name"], form.Fields.Select(f => f.Name));
        Assert.Equal(BlazorFormFieldType.Custom, BlazorFormFieldTypeResolver.Resolve(typeof(byte[])));
        Assert.Equal(BlazorFormFieldType.Custom, BlazorFormFieldTypeResolver.Resolve(typeof(MemoryStream)));
        Assert.Equal(BlazorFormFieldType.File, BlazorFormFieldTypeResolver.Resolve(typeof(IBrowserFile)));
        Assert.Equal(BlazorFormFieldType.File, BlazorFormFieldTypeResolver.Resolve(typeof(List<IBrowserFile>)));
    }

    [Fact]
    public void FileExtensions_on_a_string_keeps_it_text_and_checks_the_name()
    {
        var field = BlazorFormSchemaGenerator.Generate<DvAnnotated>().FindField("FileName")!;

        Assert.Equal(BlazorFormFieldType.Text, field.Type);
        Assert.True(Valid(field, "report.PDF"));
        Assert.True(Valid(field, "notes.docx"));
        Assert.True(Valid(field, ""));
        Assert.False(Valid(field, "virus.exe"));
    }

    [Fact]
    public void FileExtensions_on_a_browser_file_is_still_a_picker()
    {
        var field = BlazorFormSchemaGenerator.Generate<DvAnnotated>().FindField("Picture")!;

        Assert.Equal(BlazorFormFieldType.File, field.Type);
        Assert.Equal(".png", field.Accept);
        Assert.False(Valid(field, new DvFile("a.gif")));
        Assert.True(Valid(field, new DvFile("a.png")));
    }

    [Fact]
    public void DataType_upload_on_a_string_does_not_make_it_a_picker()
        => Assert.Equal(BlazorFormFieldType.Text, BlazorFormSchemaGenerator.Generate<DvAnnotated>().FindField("UploadName")!.Type);

    // ---- #12: a timestamp round-trips to the same instant

    // Every offset here is stated explicitly, so none of these depends on the machine's time zone.

    [Theory]
    [InlineData(5, 45)]
    [InlineData(-9, 30)]
    [InlineData(14, 0)]
    [InlineData(-12, 0)]
    public void A_DateTimeOffset_is_shown_on_its_own_clock_and_keeps_its_offset(int hours, int minutes)
    {
        var offset = new TimeSpan(hours, hours < 0 ? -minutes : minutes, 0);
        var original = new DateTimeOffset(2024, 1, 15, 10, 30, 0, offset);

        var text = BlazorFormValueConverter.ToInputString(original, BlazorFormFieldType.DateTime);
        Assert.Equal("2024-01-15T10:30", text);
        Assert.Equal("2024-01-15", BlazorFormValueConverter.ToInputString(original, BlazorFormFieldType.Date));

        var unchanged = BlazorFormValueConverter.FromInputString(text, typeof(DateTimeOffset), BlazorFormFieldType.DateTime, original);
        Assert.Equal(original, unchanged);
        Assert.Equal(offset, ((DateTimeOffset)unchanged!).Offset);

        var edited = BlazorFormValueConverter.FromInputString("2024-01-15T11:00", typeof(DateTimeOffset?), BlazorFormFieldType.DateTime, original);
        Assert.Equal(new DateTimeOffset(2024, 1, 15, 11, 0, 0, offset), edited);
    }

    [Fact]
    public void A_DateTimeOffset_with_no_current_value_takes_the_local_offset_of_that_moment()
    {
        var parsed = Assert.IsType<DateTimeOffset>(
            BlazorFormValueConverter.FromInputString("2024-07-01T09:00", typeof(DateTimeOffset), BlazorFormFieldType.DateTime));

        var clock = new DateTime(2024, 7, 1, 9, 0, 0);
        Assert.Equal(new DateTimeOffset(clock, TimeZoneInfo.Local.GetUtcOffset(clock)), parsed);
    }

    [Fact]
    public void A_DateTimeOffset_stated_with_its_own_offset_is_taken_at_its_word()
    {
        var current = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.FromHours(9));
        var parsed = BlazorFormValueConverter.FromInputString("2024-01-15T10:30+02:00", typeof(DateTimeOffset), BlazorFormFieldType.Text, current);

        Assert.Equal(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.FromHours(2)), parsed);
    }

    [Fact]
    public async Task Editing_a_DateTimeOffset_through_its_input_keeps_the_offset()
    {
        var model = new DvStamped { When = new DateTimeOffset(2024, 1, 15, 10, 30, 0, new TimeSpan(5, 45, 0)) };
        var form = BlazorFormBuilder.For<DvStamped>().Field(x => x.When).Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));
        var context = new BlazorFormFieldContext(state, form.Fields[0], "When");

        await context.SetFromStringAsync("2024-01-15T11:00");

        Assert.Equal(new DateTimeOffset(2024, 1, 15, 11, 0, 0, new TimeSpan(5, 45, 0)), model.When);
    }

    [Fact]
    public void A_DateTimeOffset_range_is_judged_on_the_values_own_clock()
    {
        // 01:00 on New Year's Day at +05:00 is still in 2024 on its own clock, whatever zone the server
        // is in — which is the date its input shows.
        var rule = new BlazorFormRangeRule(new DateTime(2024, 1, 1).ToOADate(), new DateTime(2024, 12, 31).ToOADate());

        Assert.True(Run(rule, new DateTimeOffset(2024, 1, 1, 1, 0, 0, TimeSpan.FromHours(5))).IsValid);
        Assert.False(Run(rule, new DateTimeOffset(2023, 12, 31, 23, 0, 0, TimeSpan.FromHours(-5))).IsValid);
    }

    // ---- #13: DataAnnotations mean what they mean to DataAnnotations

    [Fact]
    public void An_exclusive_Range_refuses_its_bounds()
    {
        var field = BlazorFormSchemaGenerator.Generate<DvAnnotated>().FindField("Ratio")!;

        Assert.False(Valid(field, 0d));
        Assert.True(Valid(field, 0.5));
        Assert.False(Valid(field, 1d));
    }

    [Fact]
    public void MinLength_on_a_collection_rendered_as_a_multi_select_counts_items()
    {
        var field = BlazorFormSchemaGenerator.Generate<DvAnnotated>().FindField("Days")!;

        Assert.Equal(BlazorFormFieldType.MultiSelect, field.Type);
        Assert.Equal(1, field.MinItems);
        Assert.False(Valid(field, new List<DayOfWeek>()));
        Assert.True(Valid(field, new List<DayOfWeek> { DayOfWeek.Monday }));
    }

    [Fact]
    public void MaxLength_on_a_file_collection_counts_files()
    {
        var field = BlazorFormSchemaGenerator.Generate<DvAnnotated>().FindField("Attachments")!;

        Assert.Equal(2, field.MaxItems);
        Assert.False(Valid(field, new List<IBrowserFile> { new DvFile("a"), new DvFile("b"), new DvFile("c") }));
    }

    [Fact]
    public void Range_text_bounds_are_read_in_the_current_culture_unless_told_otherwise()
    {
        var (price, invariant) = InCulture("de-DE", () =>
        {
            var form = BlazorFormSchemaGenerator.Generate<DvCultureBounds>();
            return (form.FindField("Price")!, form.FindField("Invariant")!);
        });

        Assert.Equal(0.01, price.Min);
        Assert.Equal(99.99, price.Max);
        Assert.Equal(0.01, invariant.Min);
        Assert.Equal(99.99, invariant.Max);
    }

    [Fact]
    public void Url_accepts_the_schemes_UrlAttribute_does()
    {
        var field = BlazorFormSchemaGenerator.Generate<DvAnnotated>().FindField("Site")!;

        Assert.True(Valid(field, "ftp://files.example.com/a.zip"));
        Assert.True(Valid(field, "https://example.com"));
        Assert.False(Valid(field, "mailto:someone@example.com"));
    }

    // ---- #14: no thousands separator in the invariant reading

    [Fact]
    public void A_decimal_comma_is_not_read_as_a_thousands_separator()
    {
        // Under a decimal-comma culture the invariant reading used to win, with the comma taken for
        // grouping. (Under en-US the culture reading still takes "1,000" as a thousand, as it should.)
        Assert.True(InCulture("de-DE", () => Run(new BlazorFormRangeRule(0, 2), "1,5").IsValid));
        Assert.True(InCulture("de-DE", () => BlazorFormConditionEvaluator.Compare("1,5", BlazorFormConditionOperator.Equals, 1.5)));
        Assert.False(InCulture("de-DE", () => BlazorFormConditionEvaluator.Compare("1,5", BlazorFormConditionOperator.Equals, 15)));
        Assert.Equal("1,5", BlazorFormValueConverter.FromInputString("1,5", typeof(double), BlazorFormFieldType.Number));
        Assert.Equal("1,5", BlazorFormValueConverter.FromInputString("1,5", null, BlazorFormFieldType.Number));
        Assert.Equal(1.5, BlazorFormValueConverter.FromInputString("1.5", typeof(double), BlazorFormFieldType.Number));
        Assert.Equal(1.5m, BlazorFormValueConverter.FromInputString("1.5", typeof(decimal), BlazorFormFieldType.Number));
    }

    // ---- #15: conditions on dates and flags

    [Fact]
    public void Equals_reads_condition_text_as_the_date_it_is_compared_with()
    {
        Assert.True(BlazorFormConditionEvaluator.Compare(new DateTime(2024, 1, 1), BlazorFormConditionOperator.Equals, "2024-01-01"));
        Assert.True(BlazorFormConditionEvaluator.Compare(new DateOnly(2024, 1, 1), BlazorFormConditionOperator.Equals, "2024-01-01"));
        Assert.False(BlazorFormConditionEvaluator.Compare(new DateOnly(2024, 1, 1), BlazorFormConditionOperator.NotEquals, "2024-01-01"));
        Assert.False(BlazorFormConditionEvaluator.Compare(new DateOnly(2024, 1, 2), BlazorFormConditionOperator.Equals, "2024-01-01"));
    }

    [Fact]
    public void Equals_keeps_its_text_and_bool_semantics()
    {
        Assert.True(BlazorFormConditionEvaluator.Compare("abc", BlazorFormConditionOperator.Equals, "ABC"));
        Assert.True(BlazorFormConditionEvaluator.Compare(true, BlazorFormConditionOperator.Equals, "true"));
        Assert.False(BlazorFormConditionEvaluator.Compare(false, BlazorFormConditionOperator.Equals, "yes"));
    }

    [Fact]
    public void Contains_on_a_flags_value_asks_whether_the_flag_is_set()
    {
        var both = DvPerm.Read | DvPerm.Write;

        Assert.True(BlazorFormConditionEvaluator.Compare(both, BlazorFormConditionOperator.Contains, "Write"));
        Assert.False(BlazorFormConditionEvaluator.Compare(DvPerm.Read, BlazorFormConditionOperator.Contains, "Write"));
        Assert.True(BlazorFormConditionEvaluator.Compare(DvPerm.Read, BlazorFormConditionOperator.NotContains, "Write"));
        Assert.True(BlazorFormConditionEvaluator.Compare(both, BlazorFormConditionOperator.Contains, 2L));
        Assert.False(BlazorFormConditionEvaluator.Compare(both, BlazorFormConditionOperator.Contains, "None"));
    }

    [Fact]
    public void A_flags_value_with_nothing_set_is_empty()
    {
        Assert.True(BlazorFormConditionEvaluator.Compare(DvPerm.None, BlazorFormConditionOperator.IsEmpty, null));
        Assert.False(BlazorFormConditionEvaluator.Compare(DvPerm.Read, BlazorFormConditionOperator.IsEmpty, null));
        Assert.False(BlazorFormConditionEvaluator.Compare(DayOfWeek.Sunday, BlazorFormConditionOperator.IsEmpty, null));
    }

    // ---- #16: Required on a flags multi-select

    [Fact]
    public async Task Required_on_a_flags_multi_select_needs_something_ticked()
    {
        var form = BlazorFormBuilder.For<DvFlagsModel>().Field(x => x.Perms, f => f.Required()).Build();
        var model = new DvFlagsModel();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        Assert.False(await state.ValidateAsync());

        model.Perms = DvPerm.Write;
        Assert.True(await state.ValidateAsync());
        Assert.True(Run(new BlazorFormRequiredRule(), DayOfWeek.Sunday).IsValid);
    }

    // ---- the pattern rule on a tag list

    [Fact]
    public void A_pattern_is_applied_to_every_tag()
    {
        var tags = new BlazorFormFieldDefinition("skills", BlazorFormFieldType.Tags);
        var rule = new BlazorFormPatternRule("^[a-z]+$");

        Assert.True(Run(rule, new List<string> { "csharp", "sql" }, tags).IsValid);
        Assert.False(Run(rule, new List<string> { "csharp", "C#" }, tags).IsValid);
        // A repeater's rows are not tags; the pattern has nothing to say about the list itself.
        Assert.True(Run(rule, new List<string> { "C#" }, new BlazorFormFieldDefinition("rows", BlazorFormFieldType.Array)).IsValid);
    }

    // ---- low-severity conversions and rules

    [Fact]
    public void An_email_with_a_trailing_newline_is_refused()
    {
        Assert.False(Run(new BlazorFormEmailRule(), "a@b.co\n").IsValid);
        Assert.True(Run(new BlazorFormEmailRule(), "a@b.co").IsValid);
    }

    [Fact]
    public void A_time_without_a_colon_is_not_read_as_days()
    {
        Assert.Equal("12", BlazorFormValueConverter.FromInputString("12", typeof(TimeSpan), BlazorFormFieldType.Time));
        Assert.Equal(new TimeSpan(12, 30, 0), BlazorFormValueConverter.FromInputString("12:30", typeof(TimeSpan), BlazorFormFieldType.Time));
    }

    [Fact]
    public void An_expression_through_Nullable_Value_binds_the_property_itself()
    {
        Assert.Equal("Age", BlazorFormExpressionPath.Of<DvNullable, int>(x => x.Age!.Value));

        var form = BlazorFormBuilder.For<DvNullable>().Field(x => x.Age!.Value).Build();
        Assert.Equal("Age", form.Fields[0].Name);
        Assert.Equal(typeof(int?), form.Fields[0].ValueType);
    }

    [Fact]
    public void An_expression_selecting_a_field_is_refused()
    {
        var ex = Assert.Throws<ArgumentException>(() => BlazorFormExpressionPath.Of<DvFieldModel, string?>(x => x.PublicField));
        Assert.Contains("PublicField", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IValidatableObject_is_called_once_with_property_attributes_included()
    {
        var model = new DvValidatable { Name = "ok" };
        var state = new BlazorFormState(BlazorFormSchemaGenerator.Generate<DvValidatable>(), new BlazorFormModelDataAccessor(model))
            .UseDataAnnotations(includePropertyAttributes: true);

        await state.ValidateAsync();
        Assert.Equal(1, model.Calls);
        Assert.Contains(state.AllMessages, m => m.Message == "Whole-model problem.");

        // An attribute failing changes nothing about how often the model's own validation runs — and
        // its complaint is still reported alongside the attribute's.
        model.Name = null;
        await state.ValidateAsync();
        Assert.Equal(2, model.Calls);
        Assert.Contains(state.AllMessages, m => m.Message == "Whole-model problem.");
        Assert.Contains(state.MessagesFor("Name"), m => m.Message.Contains("required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Class_level_attributes_run_only_once_the_property_attributes_pass()
    {
        // DataAnnotations' own order. Run regardless, the class-level check below dereferenced the null
        // [Required] had just reported and threw out of the validation pass.
        var form = BlazorFormBuilder.Create().Text("Start").Build();
        var model = new DvRanged();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model))
            .UseDataAnnotations(includePropertyAttributes: true);

        Assert.False(await state.ValidateAsync());
        Assert.NotEmpty(state.MessagesFor("Start"));
        Assert.DoesNotContain(state.AllMessages, m => m.Message == "Start is too short.");

        model.Start = "x";
        Assert.False(await state.ValidateAsync());
        Assert.Contains(state.AllMessages, m => m.Message == "Start is too short.");

        model.Start = "xy";
        Assert.True(await state.ValidateAsync());
    }

    [Fact]
    public void Compare_treats_equal_numbers_of_different_types_as_equal()
    {
        var data = new BlazorFormDictionaryDataAccessor(new Dictionary<string, object?> { ["pin"] = 5L });

        Assert.True(Run(new BlazorFormCompareRule("pin"), 5, data: data).IsValid);
        Assert.False(Run(new BlazorFormCompareRule("pin"), 6, data: data).IsValid);
        Assert.False(Run(new BlazorFormCompareRule("pin"), "5", data: data).IsValid);
    }

    [Fact]
    public void A_fraction_is_not_rounded_into_a_whole_number()
    {
        Assert.False(BlazorFormValueConverter.TryCoerce(2.5, typeof(int), out _));
        Assert.False(BlazorFormValueConverter.TryCoerce(3.7m, typeof(long), out _));
        Assert.True(BlazorFormValueConverter.TryCoerce(3.0, typeof(int), out var whole));
        Assert.Equal(3, whole);
    }

    [Fact]
    public void An_undeclared_number_is_not_an_enum_member()
    {
        Assert.Equal("999", BlazorFormValueConverter.FromInputString("999", typeof(DayOfWeek), BlazorFormFieldType.Select));
        Assert.False(BlazorFormValueConverter.TryCoerce(999L, typeof(DayOfWeek), out _));
        Assert.Equal(DayOfWeek.Monday, BlazorFormValueConverter.FromInputString("1", typeof(DayOfWeek), BlazorFormFieldType.Select));
        // A [Flags] value is a combination by nature, so any of its bits may be set together.
        Assert.Equal(DvPerm.Read | DvPerm.Write, BlazorFormValueConverter.FromInputString("3", typeof(DvPerm), BlazorFormFieldType.MultiSelect));
    }
}

/// <summary>
/// The rendered side of keeping a class-typed group or list editable when its setter is not public:
/// a read-only container now locks everything inside it, so getting the flag wrong locks the page.
/// </summary>
public class DataValidationRenderingTests : ComponentTestBase
{
    [Fact]
    public void Fields_inside_a_group_or_list_with_a_private_setter_render_editable()
    {
        var model = new DvAggregateView();
        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, BlazorFormSchemaGenerator.Generate<DvAggregateView>())
            .Add(x => x.Model, model));

        var inputs = cut.FindAll("input");
        Assert.NotEmpty(inputs);
        Assert.All(inputs, input => Assert.False(input.HasAttribute("readonly"), input.OuterHtml));
        Assert.False(cut.Find("button.ff-btn--add").HasAttribute("disabled"));
    }
}
