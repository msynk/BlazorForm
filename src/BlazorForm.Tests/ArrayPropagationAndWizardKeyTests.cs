using Bunit;

namespace BlazorForm.Tests;

/// <summary>
/// Adding or removing a row changes the value the list binds to as surely as typing does, so it has
/// to run the same sweeps. Emptying a repeater used to tell only half the engine about itself.
/// </summary>
public class ArrayChangePropagationTests
{
    private static BlazorFormDefinition WithHiddenSummary() => BlazorFormBuilder.Create()
        .ArrayOf("lines", BlazorFormFieldType.Text)
        .Text("summary", f => f
            .VisibleWhen("lines", BlazorFormConditionOperator.IsNotEmpty)
            .ClearOnHide())
        .Build();

    private static BlazorFormState Form(BlazorFormDefinition form)
        => new(form, new BlazorFormDictionaryDataAccessor());

    [Fact]
    public void Emptying_the_list_clears_a_field_the_list_was_keeping_visible()
    {
        var form = WithHiddenSummary();
        var state = Form(form);
        var lines = form.FindField("lines")!;
        state.AddArrayItem(lines, "lines");
        state.SetValue("summary", "one line so far");

        state.RemoveArrayItem("lines", 0);

        Assert.Null(state.GetValue("summary"));
    }

    [Fact]
    public void Clearing_the_list_in_one_go_does_the_same()
    {
        var form = WithHiddenSummary();
        var state = Form(form);
        var lines = form.FindField("lines")!;
        state.AddArrayItem(lines, "lines");
        state.SetValue("summary", "one line so far");

        state.ClearArrayItems("lines");

        Assert.Null(state.GetValue("summary"));
    }

    [Fact]
    public async Task A_cascading_select_that_reads_the_list_reloads_when_a_row_is_added()
    {
        var loads = 0;
        var form = BlazorFormBuilder.Create()
            .ArrayOf("lines", BlazorFormFieldType.Text)
            .Select("courier", f => f.OptionsFrom(_ =>
            {
                loads++;
                return new ValueTask<IReadOnlyList<BlazorFormSelectOption>>(
                    new List<BlazorFormSelectOption> { new("dhl", "DHL") });
            }, "lines"))
            .Build();

        var state = Form(form);
        var courier = form.FindField("courier")!;

        // The renderer loads once, then reloads whenever the dependency changes.
        await state.EnsureOptionsAsync(courier, "courier");
        Assert.Equal(1, loads);

        state.AddArrayItem(form.FindField("lines")!, "lines");
        await state.EnsureOptionsAsync(courier, "courier");

        Assert.Equal(2, loads);
    }

    [Fact]
    public void A_computed_total_still_follows_the_rows()
    {
        var form = BlazorFormBuilder.Create()
            .ArrayOf("lines", BlazorFormFieldType.Integer)
            .Field("count", BlazorFormFieldType.Integer, f => f
                .ReadOnly()
                .Computed(ctx => ctx.Value("lines") is System.Collections.ICollection c ? c.Count : 0, "lines"))
            .Build();

        var state = Form(form);
        var lines = form.FindField("lines")!;

        state.AddArrayItem(lines, "lines");
        state.AddArrayItem(lines, "lines");
        Assert.Equal(2, state.GetValue("count"));

        state.RemoveArrayItem("lines", 0);
        Assert.Equal(1, state.GetValue("count"));
    }

    [Fact]
    public void Duplicating_a_row_announces_the_list_once_it_holds_the_copy()
    {
        var form = BlazorFormBuilder.Create()
            .Array("lines", row => row.Field("name", BlazorFormFieldType.Text),
                f => f.Attr("duplicable", true))
            .Build();

        var state = Form(form);
        var lines = form.FindField("lines")!;
        state.AddArrayItem(lines, "lines");
        state.SetValue("lines[0].name", "first");

        var announced = new List<string>();
        state.FieldChanged += announced.Add;
        state.DuplicateArrayItem(lines, "lines", 0);

        Assert.Equal(["lines"], announced);
        Assert.Equal("first", state.GetValue("lines[1].name"));
    }
}

/// <summary>
/// Enter in a text box submits the form the browser thinks it belongs to. Halfway through a wizard,
/// that is not what the user meant.
/// </summary>
public class WizardImplicitSubmitTests : ComponentTestBase
{
    private static BlazorFormDefinition Wizard() => BlazorFormBuilder.Create()
        .Text("name", f => f.Required())
        .Text("city", f => f.Required())
        .Step("who", s => s.Title("Who").Fields("name"))
        .Step("where", s => s.Title("Where").Fields("city"))
        .Build();

    [Fact]
    public async Task Enter_on_a_step_that_is_not_the_last_advances_instead_of_submitting()
    {
        var state = new BlazorFormState(Wizard(), new BlazorFormDictionaryDataAccessor());
        state.SetValue("name", "Ada");
        var submitted = 0;
        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.State, state)
            .Add(x => x.OnValidSubmit, _ => submitted++));

        await cut.Find("form").SubmitAsync();

        Assert.Equal(0, submitted);
        Assert.Equal(1, state.CurrentStepIndex);
        // Nothing was "submitted", so nothing counts as a submit attempt either.
        Assert.Equal(0, state.SubmitCount);
    }

    [Fact]
    public async Task Enter_on_a_step_with_an_unanswered_question_stays_put_rather_than_reporting_the_whole_form()
    {
        var state = new BlazorFormState(Wizard(), new BlazorFormDictionaryDataAccessor());
        var cut = Render<BlazorFormView>(p => p.Add(x => x.State, state));

        await cut.Find("form").SubmitAsync();

        Assert.Equal(0, state.CurrentStepIndex);
        // The step's own field is reported; the one on a page the user has not reached is not.
        Assert.Single(state.MessagesFor("name"));
        Assert.Empty(state.MessagesFor("city"));
    }

    [Fact]
    public async Task Enter_on_the_last_step_submits()
    {
        var state = new BlazorFormState(Wizard(), new BlazorFormDictionaryDataAccessor());
        state.SetValue("name", "Ada");
        state.SetValue("city", "London");
        await state.NextStepAsync();

        var submitted = 0;
        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.State, state)
            .Add(x => x.OnValidSubmit, _ => submitted++));

        await cut.Find("form").SubmitAsync();

        Assert.Equal(1, submitted);
    }

    [Fact]
    public async Task A_single_page_form_is_untouched_by_any_of_this()
    {
        var form = BlazorFormBuilder.Create().Text("name", f => f.Required()).Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        state.SetValue("name", "Ada");

        var submitted = 0;
        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.State, state)
            .Add(x => x.OnValidSubmit, _ => submitted++));

        await cut.Find("form").SubmitAsync();

        Assert.Equal(1, submitted);
    }
}

/// <summary>
/// Debouncing exists so a remote check can run once per pause instead of once per character. It had
/// been doing the waiting without ever running the check.
/// </summary>
public class DebouncedAsyncValidationTests : ComponentTestBase
{
    [Fact]
    public async Task A_pause_runs_the_async_rules_the_keystrokes_skipped()
    {
        var checks = 0;
        var form = BlazorFormBuilder.Create()
            .Text("username", f => f
                .UpdateOnInput(debounceMilliseconds: 20)
                .MustAsync(ctx =>
                {
                    checks++;
                    return new ValueTask<bool>((ctx.Value as string) != "taken");
                }, "That username is taken."))
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        var cut = Render<BlazorFormView>(p => p.Add(x => x.State, state));
        var input = cut.Find("input#ff_username");

        await input.InputAsync(new() { Value = "taken" });
        await WaitFor(() => checks > 0);

        Assert.Equal("taken", state.GetValue("username"));
        Assert.Single(state.MessagesFor("username"));
    }

    [Fact]
    public async Task An_undebounced_field_still_waits_for_blur_rather_than_calling_out_per_character()
    {
        var checks = 0;
        var form = BlazorFormBuilder.Create()
            .Text("username", f => f
                .UpdateOnInput()
                .MustAsync(_ =>
                {
                    checks++;
                    return new ValueTask<bool>(false);
                }, "Taken."))
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        var cut = Render<BlazorFormView>(p => p.Add(x => x.State, state));
        var input = cut.Find("input#ff_username");

        await input.InputAsync(new() { Value = "a" });
        await input.InputAsync(new() { Value = "ab" });

        Assert.Equal(0, checks);

        await input.BlurAsync(new());

        Assert.True(checks > 0);
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++) await Task.Delay(10);
        Assert.True(condition(), "the condition never became true");
    }
}

/// <summary>The remaining round-2 corrections: server errors, step diagnostics and the busy button.</summary>
public class ServerErrorAndDiagnosticTests : ComponentTestBase
{
    [Fact]
    public void Server_errors_do_not_take_a_conversion_failure_with_them()
    {
        var form = BlazorFormBuilder.Create()
            .Field("age", BlazorFormFieldType.Integer)
            .Text("name")
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(new Person()));

        // "abc" cannot reach an int: the model keeps its last value and the form says so.
        state.SetValue("age", "abc");
        Assert.Single(state.MessagesFor("age"));

        state.SetServerErrors([new BlazorFormValidationMessage("name", "That name is taken.")]);

        Assert.Single(state.MessagesFor("name"));
        // The server has no opinion about a value that never reached it, and the box still shows it.
        Assert.Single(state.MessagesFor("age"));
    }

    private sealed class Person
    {
        public int Age { get; set; }
        public string? Name { get; set; }
    }

    [Fact]
    public void A_step_condition_pointing_at_nothing_is_reported()
    {
        var form = BlazorFormBuilder.Create()
            .Text("name")
            .Step("who", s => s.Title("Who").Fields("name")
                .VisibleWhen("nope", BlazorFormConditionOperator.IsTrue))
            .Build();

        Assert.Contains(form.Validate(), d => d.Message.Contains("VisibleWhen", StringComparison.Ordinal)
                                              && d.Message.Contains("nope", StringComparison.Ordinal));
    }

    [Fact]
    public void A_step_condition_that_does_resolve_is_not_reported()
    {
        var form = BlazorFormBuilder.Create()
            .Checkbox("business")
            .Text("vat")
            .Step("who", s => s.Title("Who").Fields("vat")
                .VisibleWhen("business", BlazorFormConditionOperator.IsTrue))
            .Build();

        Assert.DoesNotContain(form.Validate(), d => d.Message.Contains("VisibleWhen", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_submit_button_says_it_is_working_rather_than_only_going_grey()
    {
        var gate = new TaskCompletionSource();
        var form = BlazorFormBuilder.Create()
            .Text("name", f => f.MustAsync(async _ => { await gate.Task; return true; }, "no"))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        var cut = Render<BlazorFormView>(p => p.Add(x => x.State, state));

        var submitting = state.SubmitAsync();
        cut.Render();

        var button = cut.Find("button[type=submit]");
        Assert.Equal("Submitting…", button.TextContent.Trim());
        Assert.True(button.HasAttribute("disabled"));

        gate.SetResult();
        await submitting;
        cut.Render();

        Assert.Equal("Submit", cut.Find("button[type=submit]").TextContent.Trim());
    }
}
