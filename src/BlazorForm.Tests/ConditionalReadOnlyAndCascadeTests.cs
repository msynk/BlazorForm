using Bunit;

namespace BlazorForm.Tests;

/// <summary>
/// "This answer is settled" and "this answer does not apply" are different things, and the difference
/// is whether a keyboard or screen-reader user can still reach the value.
/// </summary>
public class ConditionalReadOnlyTests : ComponentTestBase
{
    private static BlazorFormDefinition Schema() => BlazorFormBuilder.Create()
        .Checkbox("sent", f => f.Label("Invoice sent"))
        .Text("reference", f => f
            .Label("Reference")
            .ReadOnlyWhen("sent", BlazorFormConditionOperator.IsTrue))
        .Build();

    private static BlazorFormState Form(BlazorFormDefinition? form = null)
        => new(form ?? Schema(), new BlazorFormDictionaryDataAccessor());

    [Fact]
    public void The_field_is_editable_while_the_condition_does_not_hold()
    {
        var state = Form();

        Assert.False(state.IsReadOnly(state.Definition.FindField("reference")!, "reference"));
    }

    [Fact]
    public void The_field_locks_once_the_condition_holds()
    {
        var state = Form();
        state.SetValue("sent", true);

        Assert.True(state.IsReadOnly(state.Definition.FindField("reference")!, "reference"));
    }

    [Fact]
    public void It_renders_as_readonly_and_not_as_disabled_so_the_value_can_still_be_read()
    {
        var state = Form();
        state.SetValue("sent", true);
        var cut = Render<BlazorFormView>(p => p.Add(x => x.State, state));

        var input = cut.Find("input#ff_reference");

        Assert.True(input.HasAttribute("readonly"));
        Assert.False(input.HasAttribute("disabled"));
    }

    [Fact]
    public void Inside_a_repeater_the_condition_means_that_row()
    {
        var form = BlazorFormBuilder.Create()
            .Array("lines", row => row
                .Field("locked", BlazorFormFieldType.Checkbox)
                .Field("note", BlazorFormFieldType.Text, f => f
                    .ReadOnlyWhen("locked", BlazorFormConditionOperator.IsTrue)))
            .Build();

        var state = Form(form);
        var lines = form.FindField("lines")!;
        state.AddArrayItem(lines, "lines");
        state.AddArrayItem(lines, "lines");
        state.SetValue("lines[0].locked", true);

        var note = form.FindByPath("lines[0].note")!;

        Assert.True(state.IsReadOnly(note, "lines[0].note"));
        Assert.False(state.IsReadOnly(note, "lines[1].note"));
    }

    [Fact]
    public void A_locked_repeater_loses_its_add_and_remove_buttons()
    {
        var form = BlazorFormBuilder.Create()
            .Checkbox("sent")
            .ArrayOf("codes", BlazorFormFieldType.Text, f => f
                .ReadOnlyWhen("sent", BlazorFormConditionOperator.IsTrue))
            .Build();

        var state = Form(form);
        state.SetValue("sent", true);
        var cut = Render<BlazorFormView>(p => p.Add(x => x.State, state));

        Assert.True(cut.Find("fieldset.ff-array").HasAttribute("disabled"));
    }

    [Fact]
    public void It_round_trips_through_JSON()
    {
        var json = BlazorFormJsonSchemaExporter.Export(Schema());
        var reimported = BlazorFormJsonSchemaImporter.Import(json);

        var condition = reimported.FindField("reference")!.ReadOnlyWhen;

        Assert.NotNull(condition);
        Assert.Contains("sent", condition.Dependencies);
    }

    [Fact]
    public void It_survives_a_clone()
        => Assert.NotNull(Schema().Clone().FindField("reference")!.ReadOnlyWhen);

    [Fact]
    public void A_condition_pointing_at_nothing_is_reported_by_the_diagnostics()
    {
        var form = BlazorFormBuilder.Create()
            .Text("reference", f => f.ReadOnlyWhen("nope", BlazorFormConditionOperator.IsTrue))
            .Build();

        Assert.Contains(form.Validate(), d => d.Message.Contains("ReadOnlyWhen", StringComparison.Ordinal));
    }
}

/// <summary>
/// A cascading select that empties a stale answer is changing the form, and everything that reacts to
/// a change has to hear about it — including the next select down the chain.
/// </summary>
public class CascadeClearPropagationTests
{
    private static BlazorFormDefinition ThreeLevelCascade() => BlazorFormBuilder.Create()
        .Select("country", f => f.Options(("fr", "France"), ("de", "Germany")))
        .Select("region", f => f.OptionsFrom(_ => new ValueTask<IReadOnlyList<BlazorFormSelectOption>>(
            new List<BlazorFormSelectOption> { new("north", "North") }), "country"))
        .Select("city", f => f.OptionsFrom(_ => new ValueTask<IReadOnlyList<BlazorFormSelectOption>>(
            new List<BlazorFormSelectOption> { new("paris", "Paris") }), "region"))
        .Build();

    private static async Task<BlazorFormState> Loaded(BlazorFormDefinition form)
    {
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        foreach (var name in new[] { "region", "city" })
            await state.EnsureOptionsAsync(form.FindField(name)!, name);
        return state;
    }

    [Fact]
    public async Task Clearing_a_level_reloads_the_level_below_it()
    {
        var form = ThreeLevelCascade();
        var state = await Loaded(form);
        state.SetValue("region", "north");
        state.SetValue("city", "paris");

        state.SetValue("country", "de");

        // The region was cleared because the country changed; the city depends on the region, so it
        // must go too — otherwise it still holds a city of the country the user just abandoned.
        Assert.Null(state.GetValue("region"));
        Assert.Null(state.GetValue("city"));
    }

    [Fact]
    public async Task Anything_watching_FieldChanged_hears_about_the_cleared_value()
    {
        var form = ThreeLevelCascade();
        var state = await Loaded(form);
        state.SetValue("region", "north");

        var seen = new List<string>();
        state.FieldChanged += seen.Add;
        state.SetValue("country", "fr");

        Assert.Contains("region", seen);
    }

    [Fact]
    public async Task A_computed_field_reading_the_cleared_value_is_recomputed()
    {
        var form = BlazorFormBuilder.Create()
            .Select("country", f => f.Options(("fr", "France"), ("de", "Germany")))
            .Select("region", f => f.OptionsFrom(_ => new ValueTask<IReadOnlyList<BlazorFormSelectOption>>(
                new List<BlazorFormSelectOption> { new("north", "North") }), "country"))
            .Text("summary", f => f
                .ReadOnly()
                .Computed(ctx => ctx.Sibling("region") as string ?? "(none)", "region"))
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        await state.EnsureOptionsAsync(form.FindField("region")!, "region");
        state.SetValue("region", "north");
        Assert.Equal("north", state.GetValue("summary"));

        state.SetValue("country", "de");

        Assert.Equal("(none)", state.GetValue("summary"));
    }

    [Fact]
    public async Task The_message_left_over_from_the_cleared_answer_goes_with_it()
    {
        var form = BlazorFormBuilder.Create()
            .Select("country", f => f.Options(("fr", "France"), ("de", "Germany")))
            .Select("region", f => f
                .Required()
                .OptionsFrom(_ => new ValueTask<IReadOnlyList<BlazorFormSelectOption>>(
                    new List<BlazorFormSelectOption> { new("north", "North") }), "country"))
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        await state.EnsureOptionsAsync(form.FindField("region")!, "region");
        state.SetValue("region", "north");
        state.SetServerError("region", "That region is closed.");

        state.SetValue("country", "de");

        // The complaint was about an answer the form has just taken away.
        Assert.Empty(state.MessagesFor("region"));
    }
}

/// <summary>
/// A spinner beside one box, not a form that declares itself busy because one remote lookup is in
/// flight.
/// </summary>
public class FieldValidatingStateTests
{
    private static BlazorFormDefinition Schema(TaskCompletionSource gate) => BlazorFormBuilder.Create()
        .Text("username", f => f.MustAsync(async _ =>
        {
            await gate.Task;
            return true;
        }, "Taken."))
        .Text("name", f => f.Required())
        .Build();

    [Fact]
    public async Task It_reports_the_field_while_an_async_rule_is_in_flight()
    {
        var gate = new TaskCompletionSource();
        var form = Schema(gate);
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        var running = state.ValidateFieldAsync(form.FindField("username")!, "username").AsTask();

        Assert.True(state.IsValidatingField("username"));
        Assert.Contains("username", state.ValidatingFields);

        gate.SetResult();
        await running;

        Assert.False(state.IsValidatingField("username"));
    }

    [Fact]
    public async Task A_field_whose_rules_are_all_synchronous_is_never_reported
        ()
    {
        var gate = new TaskCompletionSource();
        gate.SetResult();
        var form = Schema(gate);
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        await state.ValidateFieldAsync(form.FindField("name")!, "name");

        Assert.False(state.IsValidatingField("name"));
        Assert.Empty(state.ValidatingFields);
    }

    [Fact]
    public async Task Skipping_the_async_rules_does_not_claim_the_field_is_being_checked()
    {
        var gate = new TaskCompletionSource();
        var form = Schema(gate);
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        await state.ValidateFieldAsync(form.FindField("username")!, "username", includeAsync: false);

        Assert.False(state.IsValidatingField("username"));
    }

    [Fact]
    public async Task GetFieldState_carries_it_alongside_touched_and_dirty()
    {
        var gate = new TaskCompletionSource();
        var form = Schema(gate);
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        var running = state.ValidateFieldAsync(form.FindField("username")!, "username").AsTask();

        Assert.True(state.GetFieldState("username").IsValidating);

        gate.SetResult();
        await running;

        Assert.False(state.GetFieldState("username").IsValidating);
    }
}

/// <summary>
/// "Saved." is a claim about the save, not about the form being free of errors.
/// </summary>
public class SubmitSuccessTests
{
    private static BlazorFormState Form() => new(
        BlazorFormBuilder.Create().Text("name", f => f.Required()).Build(),
        new BlazorFormDictionaryDataAccessor());

    [Fact]
    public void A_form_nobody_has_submitted_makes_no_claim()
        => Assert.False(Form().IsSubmitSuccessful);

    [Fact]
    public async Task A_submit_that_failed_validation_is_not_a_success()
    {
        var state = Form();

        await state.SubmitAsync();

        Assert.False(state.IsSubmitSuccessful);
    }

    [Fact]
    public async Task A_submit_whose_handler_returned_is()
    {
        var state = Form();
        state.SetValue("name", "Ada");

        await state.SubmitAsync(_ => Task.CompletedTask);

        Assert.True(state.IsSubmitSuccessful);
    }

    [Fact]
    public async Task A_handler_that_threw_did_not_save_anything()
    {
        var state = Form();
        state.SetValue("name", "Ada");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => state.SubmitAsync(_ => throw new InvalidOperationException("the server said no")));

        Assert.False(state.IsSubmitSuccessful);
    }

    [Fact]
    public async Task The_next_attempt_withdraws_the_previous_claim_before_it_is_judged()
    {
        var state = Form();
        state.SetValue("name", "Ada");
        await state.SubmitAsync(_ => Task.CompletedTask);

        state.SetValue("name", "");
        await state.SubmitAsync(_ => Task.CompletedTask);

        Assert.False(state.IsSubmitSuccessful);
    }

    [Fact]
    public async Task Resetting_the_form_withdraws_it_too()
    {
        var state = Form();
        state.SetValue("name", "Ada");
        await state.SubmitAsync(_ => Task.CompletedTask);

        state.Reset();

        Assert.False(state.IsSubmitSuccessful);
    }
}
