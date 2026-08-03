using System.ComponentModel.DataAnnotations;
using Bunit;

namespace BlazorForm.Tests;

public class OptionalRow
{
    public string Kind { get; set; } = "";
    public string? Email { get; set; }
}

public class OptionalRowSheet
{
    /// <summary>A root field sharing a name with one inside every row. This is where the bug lived.</summary>
    public string? Email { get; set; }

    /// <summary>A root-only field, so the fallback the scope was written for still has a case to prove.</summary>
    public string? SheetName { get; set; }

    public List<OptionalRow> Rows { get; set; } = [];
}

/// <summary>
/// A scoped read has to choose between the row's field and the root's. Choosing on "did the scoped
/// read come back null" is wrong, because a row's own field is allowed to be empty — and when it is,
/// the root's field of the same name would answer for it.
/// </summary>
public class ScopedReadExistenceTests
{
    private static BlazorFormState Form(OptionalRowSheet model)
    {
        var schema = BlazorFormBuilder.For<OptionalRowSheet>()
            .Field(x => x.Email)
            .Array(x => x.Rows, row => row
                .Field(r => r.Kind)
                .Field(r => r.Email, f => f.VisibleWhen("Email", BlazorFormConditionOperator.IsNotEmpty)))
            .Build();

        return new BlazorFormState(schema, new BlazorFormModelDataAccessor(model));
    }

    [Fact]
    public void An_empty_row_field_is_not_answered_for_by_the_root()
    {
        var model = new OptionalRowSheet
        {
            Email = "root@example.com",
            Rows = [new OptionalRow { Email = null }]
        };
        var state = Form(model);
        var rowEmail = state.Definition.FindField("Rows")!.ItemTemplate!.Children.Single(c => c.Name == "Email");

        // The row's own Email is empty, so the condition is false — regardless of what the root holds.
        Assert.False(state.IsVisible(rowEmail, "Rows[0].Email"));
    }

    [Fact]
    public void A_filled_row_field_still_wins_over_the_root()
    {
        var model = new OptionalRowSheet
        {
            Email = null,
            Rows = [new OptionalRow { Email = "row@example.com" }]
        };
        var state = Form(model);
        var rowEmail = state.Definition.FindField("Rows")!.ItemTemplate!.Children.Single(c => c.Name == "Email");

        Assert.True(state.IsVisible(rowEmail, "Rows[0].Email"));
    }

    [Fact]
    public void A_name_the_row_does_not_have_still_falls_back_to_the_root()
    {
        // This is what the fallback was written for, and it has to keep working: an absolute reference
        // from inside a repeater to a field that only exists at the top level.
        var model = new OptionalRowSheet { SheetName = "Q3 leads", Rows = [new OptionalRow()] };
        var scoped = BlazorFormScopedDataReader.ForOwnerOf(
            new BlazorFormModelDataAccessor(model), "Rows[0].Kind");

        // A row has no SheetName of its own, so the reference can only mean the sheet's.
        Assert.Equal("Q3 leads", scoped.GetValue("SheetName"));
        Assert.Null(scoped.GetValue("Nonexistent"));
    }

    [Fact]
    public void TryGetValue_tells_absent_apart_from_empty()
    {
        var accessor = new BlazorFormModelDataAccessor(new OptionalRowSheet { Rows = [new OptionalRow()] });

        Assert.True(accessor.TryGetValue("Rows[0].Email", out var present));
        Assert.Null(present);

        Assert.False(accessor.TryGetValue("Rows[0].Nonexistent", out _));
        Assert.False(accessor.TryGetValue("Rows[5].Email", out _));
    }

    [Fact]
    public void The_dictionary_store_reports_a_missing_key_as_missing()
    {
        var accessor = new BlazorFormDictionaryDataAccessor(new Dictionary<string, object?> { ["a"] = null });

        Assert.True(accessor.TryGetValue("a", out var present));
        Assert.Null(present);
        Assert.False(accessor.TryGetValue("b", out _));
    }
}

public class CollectionAnnotationsModel
{
    [MinLength(1), MaxLength(3)]
    public List<string> Tags { get; set; } = [];

    [MinLength(2)]
    public string Name { get; set; } = "";
}

/// <summary>
/// [MinLength] and [MaxLength] mean item counts on a collection, exactly as [Length] does. Mapping
/// them to the string rule made "at least one tag" pass on an empty list — silently, which is the
/// worst way for a rule to fail.
/// </summary>
public class CollectionLengthAnnotationTests
{
    private static BlazorFormState Form(CollectionAnnotationsModel model)
        => new(BlazorFormSchemaGenerator.Generate<CollectionAnnotationsModel>(),
            new BlazorFormModelDataAccessor(model));

    [Fact]
    public async Task An_empty_list_fails_its_minimum()
    {
        var state = Form(new CollectionAnnotationsModel { Name = "Ada" });

        await state.ValidateAsync();

        Assert.NotEmpty(state.MessagesFor("Tags"));
    }

    [Fact]
    public async Task A_list_within_bounds_passes()
    {
        var state = Form(new CollectionAnnotationsModel { Name = "Ada", Tags = ["one", "two"] });

        await state.ValidateAsync();

        Assert.Empty(state.MessagesFor("Tags"));
    }

    [Fact]
    public async Task Too_many_items_fails_the_maximum()
    {
        var state = Form(new CollectionAnnotationsModel { Name = "Ada", Tags = ["a", "b", "c", "d"] });

        await state.ValidateAsync();

        Assert.NotEmpty(state.MessagesFor("Tags"));
    }

    [Fact]
    public void The_bounds_reach_the_schema_as_item_counts()
    {
        var form = BlazorFormSchemaGenerator.Generate<CollectionAnnotationsModel>();
        var tags = form.FindField("Tags")!;

        Assert.Equal(1, tags.MinItems);
        Assert.Equal(3, tags.MaxItems);
        Assert.Null(tags.MinLength);
    }

    [Fact]
    public async Task A_string_is_still_measured_in_characters()
    {
        var state = Form(new CollectionAnnotationsModel { Name = "A", Tags = ["one"] });

        await state.ValidateAsync();

        Assert.NotEmpty(state.MessagesFor("Name"));
    }
}

public class ContradictoryBoundsTests
{
    [Fact]
    public void A_range_that_crosses_over_is_reported()
    {
        var form = BlazorFormBuilder.Create().Number("n", f => f.Range(10, 5)).Build();

        Assert.Contains(form.Validate(), d => d.Message.Contains("Min (10) is greater than Max (5)", StringComparison.Ordinal));
    }

    [Fact]
    public void So_are_crossed_lengths_and_item_counts()
    {
        var form = BlazorFormBuilder.Create()
            .Text("s", f => f.MinLength(20).MaxLength(5))
            .ArrayOf("xs", BlazorFormFieldType.Text, f => f.Items(min: 4, max: 2))
            .Build();

        Assert.Equal(2, form.Validate().Count(d => d.Message.Contains("no value can satisfy both", StringComparison.Ordinal)));
    }

    [Fact]
    public void An_editable_computed_field_is_reported()
    {
        // Anything typed into it is overwritten the next time a dependency changes.
        var form = BlazorFormBuilder.Create()
            .Number("total", f => f.Computed(_ => 0))
            .Build();

        Assert.Contains(form.Validate(), d => d.Message.Contains("computed field is editable", StringComparison.Ordinal));
    }

    [Fact]
    public void A_sound_schema_reports_nothing()
    {
        var form = BlazorFormBuilder.Create()
            .Number("n", f => f.Range(5, 10))
            .Text("s", f => f.MinLength(2).MaxLength(50))
            .Build();

        Assert.Empty(form.Validate());
    }
}

public class DisabledFormTests : ComponentTestBase
{
    private static BlazorFormDefinition Schema() => BlazorFormBuilder.Create()
        .Text("a")
        .Select("b", f => f.Options(("x", "X")))
        .Build();

    [Fact]
    public void A_disabled_form_disables_its_controls_and_its_buttons()
    {
        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, Schema())
            .Add(x => x.Disabled, true));

        Assert.True(cut.Find("input#ff_a").HasAttribute("disabled"));
        Assert.True(cut.Find("select#ff_b").HasAttribute("disabled"));
        Assert.True(cut.Find("button[type=submit]").HasAttribute("disabled"));
    }

    [Fact]
    public void Disabled_is_not_the_same_as_busy()
    {
        // aria-busy means "working on it", which a locked form is not.
        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, Schema())
            .Add(x => x.Disabled, true));

        Assert.False(cut.Find("form").HasAttribute("aria-busy"));
    }

    [Fact]
    public void Turning_it_back_off_re_enables_everything()
    {
        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, Schema())
            .Add(x => x.Disabled, true));

        cut.Render(p => p.Add(x => x.Definition, cut.Instance.Form.Definition).Add(x => x.Disabled, false));

        Assert.False(cut.Find("input#ff_a").HasAttribute("disabled"));
    }
}

public class CharacterCountIsNonDestructiveTests : ComponentTestBase
{
    [Fact]
    public void A_counted_field_drops_its_maxlength_attribute()
    {
        // maxlength silently truncates a pasted answer. A counter plus the length rule lets the user
        // keep their text and edit it down, which is the whole reason to show a count.
        var form = BlazorFormBuilder.Create()
            .Text("title", f => f.MaxLength(60).CharacterCount())
            .Build();

        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, form));

        Assert.False(cut.Find("input#ff_title").HasAttribute("maxlength"));
    }

    [Fact]
    public void A_field_without_a_counter_keeps_it()
    {
        var form = BlazorFormBuilder.Create().Text("title", f => f.MaxLength(60)).Build();
        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, form));

        Assert.Equal("60", cut.Find("input#ff_title").GetAttribute("maxlength"));
    }

    [Fact]
    public async Task Going_over_the_limit_is_still_an_error()
    {
        var form = BlazorFormBuilder.Create()
            .Text("title", f => f.MaxLength(5).CharacterCount())
            .Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        state.SetValue("title", "far too long");
        await state.ValidateAsync();

        Assert.NotEmpty(state.MessagesFor("title"));
    }
}

public class RangeAnnouncementTests : ComponentTestBase
{
    [Fact]
    public void A_slider_with_a_unit_announces_the_unit()
    {
        var form = BlazorFormBuilder.Create()
            .Field("weight", BlazorFormFieldType.Range, f => f.Range(0, 200).Suffix(" kg"))
            .Build();
        var data = new Dictionary<string, object?> { ["weight"] = 70 };

        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, form).Add(x => x.Data, data));

        Assert.Equal("70 kg", cut.Find("input#ff_weight").GetAttribute("aria-valuetext"));
    }

    [Fact]
    public void A_bare_slider_leaves_the_announcement_to_the_browser()
    {
        var form = BlazorFormBuilder.Create()
            .Field("n", BlazorFormFieldType.Range, f => f.Range(0, 10))
            .Build();

        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, form));

        Assert.False(cut.Find("input#ff_n").HasAttribute("aria-valuetext"));
    }
}
