using Bunit;

namespace BlazorForm.Tests;

/// <summary>
/// A combobox is a select you can type into. It is built on the browser's own
/// <c>&lt;input list&gt;</c>, so filtering, keyboard navigation and screen-reader support come from
/// the platform; what the library adds is the mapping between the label the user reads and the value
/// the model stores, which is the only thing a bare datalist cannot do.
/// </summary>
public class ComboboxRenderingTests : ComponentTestBase
{
    private static BlazorFormDefinition Schema(bool allowCustom = false) => BlazorFormBuilder.Create()
        .Combobox("country", f => f
            .Label("Country")
            .Options(("fr", "France"), ("gb", "United Kingdom"), ("de", "Germany"))
            .AsCombobox(allowCustom))
        .Build();

    private Bunit.IRenderedComponent<BlazorFormView> RenderForm(BlazorFormDefinition form, BlazorFormState? state = null)
        => state is null
            ? Render<BlazorFormView>(p => p.Add(x => x.Definition, form))
            : Render<BlazorFormView>(p => p.Add(x => x.State, state));

    [Fact]
    public void It_renders_an_input_bound_to_a_datalist_of_the_options()
    {
        var cut = RenderForm(Schema());
        var input = cut.Find("input#ff_country");

        Assert.Equal("combobox", input.GetAttribute("role"));
        Assert.Equal("ff_country_list", input.GetAttribute("list"));
        Assert.Equal("list", input.GetAttribute("aria-autocomplete"));
        Assert.Equal(3, cut.FindAll("#ff_country_list option").Count);
    }

    [Fact]
    public void The_datalist_offers_labels_because_that_is_what_the_browser_puts_in_the_box()
    {
        var cut = RenderForm(Schema());

        var values = cut.FindAll("#ff_country_list option").Select(o => o.GetAttribute("value")).ToList();

        Assert.Equal(["France", "United Kingdom", "Germany"], values);
    }

    [Fact]
    public void Choosing_a_label_stores_the_options_value()
    {
        var state = new BlazorFormState(Schema(), new BlazorFormDictionaryDataAccessor());
        var cut = RenderForm(Schema(), state);

        cut.Find("input#ff_country").Change("United Kingdom");

        Assert.Equal("gb", state.GetValue("country"));
    }

    [Fact]
    public void The_box_shows_the_label_of_whatever_value_the_model_holds()
    {
        var state = new BlazorFormState(Schema(), new BlazorFormDictionaryDataAccessor());
        state.SetValue("country", "de");

        var cut = RenderForm(Schema(), state);

        Assert.Equal("Germany", cut.Find("input#ff_country").GetAttribute("value"));
    }

    [Fact]
    public void Matching_a_label_ignores_case_because_the_user_is_typing_it()
    {
        var state = new BlazorFormState(Schema(), new BlazorFormDictionaryDataAccessor());
        var cut = RenderForm(Schema(), state);

        cut.Find("input#ff_country").Change("france");

        Assert.Equal("fr", state.GetValue("country"));
    }

    [Fact]
    public void An_entry_on_no_list_is_kept_rather_than_discarded()
    {
        // The same promise the rest of the library makes: what the user typed is never thrown away
        // without saying so. The rule below is what says so.
        var state = new BlazorFormState(Schema(), new BlazorFormDictionaryDataAccessor());
        var cut = RenderForm(Schema(), state);

        cut.Find("input#ff_country").Change("Atlantis");

        Assert.Equal("Atlantis", state.GetValue("country"));
        Assert.Equal("Atlantis", cut.Find("input#ff_country").GetAttribute("value"));
    }

    [Fact]
    public void Clearing_the_box_clears_the_value()
    {
        var state = new BlazorFormState(Schema(), new BlazorFormDictionaryDataAccessor());
        state.SetValue("country", "fr");
        var cut = RenderForm(Schema(), state);

        cut.Find("input#ff_country").Change("");

        Assert.Null(state.GetValue("country"));
    }

    [Fact]
    public void A_disabled_option_is_not_offered_at_all()
    {
        // A datalist has no disabled state, so offering a choice the field will then reject is worse
        // than leaving it out.
        var form = BlazorFormBuilder.Create()
            .Combobox("x", f => f.Options(
                new BlazorFormSelectOption("a", "Available"),
                new BlazorFormSelectOption("b", "Sold out", Disabled: true)))
            .Build();

        Assert.Single(RenderForm(form).FindAll("#ff_x_list option"));
    }
}

public class ComboboxValidationTests
{
    private static BlazorFormDefinition Closed() => BlazorFormBuilder.Create()
        .Combobox("country", f => f.Options(("fr", "France"), ("gb", "United Kingdom")).AsCombobox())
        .Build();

    [Fact]
    public async Task An_answer_that_is_on_no_list_is_reported()
    {
        var state = new BlazorFormState(Closed(), new BlazorFormDictionaryDataAccessor());
        state.SetValue("country", "Atlantis");

        Assert.False(await state.ValidateAsync());
        Assert.NotEmpty(state.MessagesFor("country"));
    }

    [Fact]
    public async Task An_answer_on_the_list_passes()
    {
        var state = new BlazorFormState(Closed(), new BlazorFormDictionaryDataAccessor());
        state.SetValue("country", "gb");

        Assert.True(await state.ValidateAsync());
    }

    [Fact]
    public async Task An_empty_answer_is_Requireds_business_not_this_rules()
    {
        var state = new BlazorFormState(Closed(), new BlazorFormDictionaryDataAccessor());

        Assert.True(await state.ValidateAsync());
    }

    [Fact]
    public async Task AllowCustom_accepts_anything()
    {
        var form = BlazorFormBuilder.Create()
            .Combobox("city", f => f.Options(("ams", "Amsterdam")).AsCombobox(allowCustom: true))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        state.SetValue("city", "Delft");

        Assert.True(await state.ValidateAsync());
    }

    [Fact]
    public async Task AllowCustom_removes_a_rule_an_earlier_call_added()
    {
        // The builder is a chain, and .AsCombobox() then .AsCombobox(true) has to mean the second one.
        var form = BlazorFormBuilder.Create()
            .Combobox("city", f => f.Options(("ams", "Amsterdam")).AsCombobox().AsCombobox(allowCustom: true))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        state.SetValue("city", "Delft");

        Assert.True(await state.ValidateAsync());
    }

    [Fact]
    public async Task Options_that_arrive_from_a_provider_are_not_judged_here()
    {
        // They live in the form's runtime state, not in the schema, so at rule time there is nothing to
        // compare against — and failing every answer would be worse than checking none.
        var form = BlazorFormBuilder.Create()
            .Combobox("city", f => f
                .OptionsFrom(_ => ValueTask.FromResult<IReadOnlyList<BlazorFormSelectOption>>(
                    [new BlazorFormSelectOption("ams", "Amsterdam")]))
                .AsCombobox())
            .Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        state.SetValue("city", "anything at all");

        Assert.True(await state.ValidateAsync());
    }

    [Fact]
    public void A_combobox_counts_as_a_choice_field_so_options_do_not_turn_it_into_a_dropdown()
    {
        var form = BlazorFormBuilder.Create()
            .Combobox("x", f => f.AsCombobox().Options(("a", "A")))
            .Build();

        Assert.Equal(BlazorFormFieldType.Combobox, form.FindField("x")!.Type);
    }
}

public class TagsInputTests : ComponentTestBase
{
    private static BlazorFormDefinition Schema(int? max = null) => BlazorFormBuilder.Create()
        .Tags("labels", f => f.Label("Labels").AsTags(max))
        .Build();

    private (Bunit.IRenderedComponent<BlazorFormView> Cut, BlazorFormState State) RenderForm(int? max = null)
    {
        var state = new BlazorFormState(Schema(max), new BlazorFormDictionaryDataAccessor());
        return (Render<BlazorFormView>(p => p.Add(x => x.State, state)), state);
    }

    private static List<string> TagsOf(BlazorFormState state)
        => state.GetValue("labels") is IEnumerable<object?> items
            ? items.Select(i => i?.ToString() ?? "").ToList()
            : [];

    [Fact]
    public void Enter_commits_a_tag()
    {
        var (cut, state) = RenderForm();

        cut.Find(".ff-tags__entry").Input("urgent");
        cut.Find(".ff-tags__entry").KeyDown("Enter");

        Assert.Equal(["urgent"], TagsOf(state));
    }

    [Fact]
    public void The_entry_box_belongs_to_no_form_so_Enter_cannot_submit_one()
    {
        // Enter is how every tag input is used. Without detaching the box from its form owner, the
        // first tag a user typed would post the form instead of being added.
        var (cut, _) = RenderForm();

        Assert.Equal("ff_labels_none", cut.Find(".ff-tags__entry").GetAttribute("form"));
    }

    [Fact]
    public void A_separator_commits_the_tag_before_it()
    {
        var (cut, state) = RenderForm();

        cut.Find(".ff-tags__entry").Input("urgent,");

        Assert.Equal(["urgent"], TagsOf(state));
    }

    [Fact]
    public void Pasting_a_separated_list_becomes_several_tags_not_one()
    {
        var (cut, state) = RenderForm();

        cut.Find(".ff-tags__entry").Input("red,green,blue");

        // The trailing fragment stays in the box until it is committed in its own right.
        Assert.Equal(["red", "green"], TagsOf(state));
    }

    [Fact]
    public void A_duplicate_is_refused_whatever_its_casing()
    {
        var (cut, state) = RenderForm();

        cut.Find(".ff-tags__entry").Input("Urgent");
        cut.Find(".ff-tags__entry").KeyDown("Enter");
        cut.Find(".ff-tags__entry").Input("urgent");
        cut.Find(".ff-tags__entry").KeyDown("Enter");

        Assert.Equal(["Urgent"], TagsOf(state));
    }

    [Fact]
    public void Whitespace_only_entries_are_ignored()
    {
        var (cut, state) = RenderForm();

        cut.Find(".ff-tags__entry").Input("   ");
        cut.Find(".ff-tags__entry").KeyDown("Enter");

        Assert.Empty(TagsOf(state));
    }

    [Fact]
    public void Each_tag_is_a_chip_with_a_remove_button_that_names_it()
    {
        var (cut, state) = RenderForm();
        state.SetValue("labels", new List<string> { "urgent", "bug" });
        cut.Render();

        var buttons = cut.FindAll(".ff-tag__remove");

        Assert.Equal(2, buttons.Count);
        Assert.Equal("Remove urgent", buttons[0].GetAttribute("aria-label"));
    }

    [Fact]
    public void Removing_a_chip_removes_the_tag()
    {
        var (cut, state) = RenderForm();
        state.SetValue("labels", new List<string> { "urgent", "bug" });
        cut.Render();

        cut.FindAll(".ff-tag__remove")[0].Click();

        Assert.Equal(["bug"], TagsOf(state));
    }

    [Fact]
    public void Backspace_on_an_empty_box_removes_the_last_tag()
    {
        var (cut, state) = RenderForm();
        state.SetValue("labels", new List<string> { "urgent", "bug" });
        cut.Render();

        cut.Find(".ff-tags__entry").KeyDown("Backspace");

        Assert.Equal(["urgent"], TagsOf(state));
    }

    [Fact]
    public void Backspace_with_something_typed_edits_the_text_instead()
    {
        var (cut, state) = RenderForm();
        state.SetValue("labels", new List<string> { "urgent" });
        cut.Render();

        cut.Find(".ff-tags__entry").Input("bu");
        cut.Find(".ff-tags__entry").KeyDown("Backspace");

        Assert.Equal(["urgent"], TagsOf(state));
    }

    [Fact]
    public void Leaving_the_box_commits_what_was_half_typed_rather_than_losing_it()
    {
        var (cut, state) = RenderForm();

        cut.Find(".ff-tags__entry").Input("urgent");
        cut.Find(".ff-tags__entry").Blur();

        Assert.Equal(["urgent"], TagsOf(state));
    }

    [Fact]
    public void A_full_list_stops_accepting_and_stops_offering_the_box()
    {
        var (cut, state) = RenderForm(max: 2);
        state.SetValue("labels", new List<string> { "a", "b" });
        cut.Render();

        Assert.Empty(cut.FindAll(".ff-tags__entry"));
        Assert.Equal(["a", "b"], TagsOf(state));
    }

    [Fact]
    public void A_read_only_field_shows_its_tags_but_offers_no_way_to_change_them()
    {
        var (cut, state) = RenderForm();
        state.SetValue("labels", new List<string> { "urgent" });
        state.ReadOnly = true;
        cut.Render();

        Assert.Single(cut.FindAll(".ff-tag"));
        Assert.Empty(cut.FindAll(".ff-tag__remove"));
        Assert.Empty(cut.FindAll(".ff-tags__entry"));
    }

    [Fact]
    public void The_group_is_labelled_and_wired_for_assistive_tech()
    {
        var (cut, _) = RenderForm();
        var group = cut.Find(".ff-tags");

        Assert.Equal("group", group.GetAttribute("role"));
        Assert.Equal("ff_labels_label", group.GetAttribute("aria-labelledby"));
    }
}

/// <summary>
/// The schema's own "and when this changes, do that" — what TanStack Form calls listeners and Formly
/// calls hooks. Neither a computed value (which owns its field outright) nor a cascading options
/// dependency (which reloads choices) can express it, because what the handler writes is still the
/// user's to edit afterwards.
/// </summary>
public class ChangeHandlerTests
{
    [Fact]
    public void A_handler_runs_when_its_field_changes()
    {
        var form = BlazorFormBuilder.Create()
            .Text("country", f => f.OnChange(ctx => ctx.ClearSibling("city")))
            .Text("city")
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        state.SetValue("city", "Paris");

        state.SetValue("country", "de");

        Assert.Null(state.GetValue("city"));
    }

    [Fact]
    public void A_handler_does_not_run_for_a_change_to_another_field()
    {
        var runs = 0;
        var form = BlazorFormBuilder.Create()
            .Text("a", f => f.OnChange(_ => runs++))
            .Text("b")
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        state.SetValue("b", "x");

        Assert.Equal(0, runs);
    }

    [Fact]
    public void A_handler_is_silent_while_the_form_is_being_constructed()
    {
        // Applying a default is the form initialising, not the user changing something. Without this a
        // handler that clears a sibling would wipe the data the form was handed.
        var runs = 0;
        var form = BlazorFormBuilder.Create()
            .Text("a", f => f.Default("seed").OnChange(_ => runs++))
            .Build();

        _ = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        Assert.Equal(0, runs);
    }

    [Fact]
    public void What_a_handler_writes_does_not_mark_the_field_touched()
    {
        // The user has not been there, so the field must not open covered in errors.
        var form = BlazorFormBuilder.Create()
            .Text("plan", f => f.OnChange(ctx => ctx.SetSibling("seats", 5)))
            .Integer("seats", f => f.Required())
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        state.SetValue("plan", "team");

        Assert.Equal(5, state.GetValue("seats"));
        Assert.False(state.IsTouched("seats"));
    }

    [Fact]
    public void Inside_a_repeater_a_handler_means_that_row()
    {
        var form = BlazorFormBuilder.Create()
            .Array("rows", row => row
                .Text("country", f => f.OnChange(ctx => ctx.ClearSibling("city")))
                .Text("city"))
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        var rows = form.FindField("rows")!;
        state.AddArrayItem(rows, "rows");
        state.AddArrayItem(rows, "rows");
        state.SetValue("rows[0].city", "Paris");
        state.SetValue("rows[1].city", "Berlin");

        state.SetValue("rows[0].country", "de");

        Assert.Null(state.GetValue("rows[0].city"));
        Assert.Equal("Berlin", state.GetValue("rows[1].city"));
    }

    [Fact]
    public void A_handler_can_read_the_new_value()
    {
        string? seen = null;
        var form = BlazorFormBuilder.Create()
            .Text("a", f => f.OnChange(ctx => seen = ctx.Value as string))
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        state.SetValue("a", "hello");

        Assert.Equal("hello", seen);
    }

    [Fact]
    public void Two_handlers_that_answer_each_other_settle_instead_of_recursing()
    {
        var form = BlazorFormBuilder.Create()
            .Integer("a", f => f.OnChange(ctx => ctx.SetSibling("b", Convert.ToInt32(ctx.Value ?? 0) + 1)))
            .Integer("b", f => f.OnChange(ctx => ctx.SetSibling("a", Convert.ToInt32(ctx.Value ?? 0) + 1)))
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        state.SetValue("a", 1);

        // No stack overflow, and both fields hold a number.
        Assert.NotNull(state.GetValue("a"));
        Assert.NotNull(state.GetValue("b"));
    }

    [Fact]
    public void A_handler_runs_for_a_computed_value_too()
    {
        // A derived value is a change like any other; the schema should not have one kind of change it
        // cannot react to.
        string? seen = null;
        var form = BlazorFormBuilder.Create()
            .Text("first")
            .Text("full", f => f
                .Computed(ctx => ctx.Sibling("first"), "first")
                .OnChange(ctx => seen = ctx.Value as string))
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        state.SetValue("first", "Ada");

        Assert.Equal("Ada", seen);
    }
}

public class NewWidgetJsonRoundTripTests
{
    [Fact]
    public void A_combobox_survives_export_and_re_import()
    {
        var form = BlazorFormBuilder.Create()
            .Combobox("country", f => f.Options(("fr", "France")).AsCombobox())
            .Build();

        var reimported = BlazorFormJsonSchemaImporter.Import(BlazorFormJsonSchemaExporter.Export(form));

        Assert.Equal(BlazorFormFieldType.Combobox, reimported.FindField("country")!.Type);
    }

    [Fact]
    public void A_tags_field_survives_export_and_re_import()
    {
        var form = BlazorFormBuilder.Create().Tags("labels", f => f.AsTags(5)).Build();

        var reimported = BlazorFormJsonSchemaImporter.Import(BlazorFormJsonSchemaExporter.Export(form));
        var field = reimported.FindField("labels")!;

        Assert.Equal(BlazorFormFieldType.Tags, field.Type);
        Assert.Equal(5, field.MaxItems);
    }
}
