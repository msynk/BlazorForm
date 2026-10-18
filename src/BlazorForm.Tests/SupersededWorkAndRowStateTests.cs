using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorForm.Tests;

public class RowStateModel
{
    public List<RowStateRow> Rows { get; set; } = [];
    public RowStateRow[] Fixed { get; set; } = [];
    public string[] Labels { get; set; } = [];
    public bool Show { get; set; } = true;
    public int Count { get; set; }
    public string? Name { get; set; }
}

public class RowStateRow
{
    public string? Country { get; set; }
    public string? City { get; set; }
    public int Qty { get; set; }
    public List<string> Tags { get; set; } = [];
}

/// <summary>
/// A run that is superseded must neither crash nor leave anything behind: the spinner it raised, the
/// in-flight flag it set, or a verdict about data nobody checked.
/// </summary>
public class SupersededWorkTests
{
    [Fact]
    public async Task A_keystroke_superseding_an_async_check_takes_its_spinner_down()
    {
        var lookup = new TaskCompletionSource<bool>();
        var form = BlazorFormBuilder.For<RowStateModel>()
            .Field(x => x.Name, f => f.MustAsync(async _ => await lookup.Task, "taken"))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(new RowStateModel()));
        var field = form.FindField("Name")!;

        var blur = state.ValidateFieldAsync(field, "Name", includeAsync: true);
        Assert.True(state.IsValidatingField("Name"));

        // The user comes back and types: a synchronous run supersedes the remote one.
        await state.ValidateFieldAsync(field, "Name", includeAsync: false);
        lookup.SetResult(true);
        await blur;

        Assert.False(state.IsValidatingField("Name"));
    }

    [Fact]
    public async Task A_superseded_full_validation_neither_throws_nor_reports_a_pass()
    {
        var lookup = new TaskCompletionSource<bool>();
        var form = BlazorFormBuilder.For<RowStateModel>()
            .Field(x => x.Name, f => f.MustAsync(async _ => await lookup.Task, "taken"))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(new RowStateModel()));

        var first = state.ValidateAsync();
        var second = state.ValidateAsync();
        lookup.SetResult(false);

        // The first run's source was disposed by the second; reading its token used to throw.
        Assert.False(await first);
        Assert.False(await second);
        Assert.Single(state.MessagesFor("Name"));
    }

    [Fact]
    public async Task A_second_Next_while_the_first_is_waiting_does_not_throw()
    {
        var lookup = new TaskCompletionSource<bool>();
        var form = BlazorFormBuilder.For<RowStateModel>()
            .Field(x => x.Name, f => f.MustAsync(async _ => await lookup.Task, "taken"))
            .Field(x => x.Count)
            .Step("one", s => s.Fields("Name"))
            .Step("two", s => s.Fields("Count"))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(new RowStateModel()));

        var first = state.NextStepAsync();
        var second = state.NextStepAsync();
        lookup.SetResult(true);

        Assert.False(await first);
        Assert.True(await second);
        Assert.Equal(1, state.CurrentStepIndex);
    }

    [Fact]
    public async Task Async_rules_are_handed_the_runs_cancellation_token()
    {
        var observed = new List<CancellationToken>();
        var lookup = new TaskCompletionSource<bool>();
        var form = BlazorFormBuilder.For<RowStateModel>()
            .Field(x => x.Name, f => f.MustAsync(async ctx =>
            {
                observed.Add(ctx.CancellationToken);
                return await lookup.Task;
            }, "taken"))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(new RowStateModel()));
        var field = form.FindField("Name")!;

        var first = state.ValidateFieldAsync(field, "Name");
        var second = state.ValidateFieldAsync(field, "Name");

        Assert.Equal(2, observed.Count);
        Assert.True(observed[0].IsCancellationRequested);
        Assert.False(observed[1].IsCancellationRequested);

        lookup.SetResult(true);
        await first;
        await second;
    }

    [Fact]
    public async Task Reset_abandons_a_validation_still_in_flight()
    {
        var lookup = new TaskCompletionSource<bool>();
        var form = BlazorFormBuilder.For<RowStateModel>()
            .Field(x => x.Name, f => f.MustAsync(async _ => await lookup.Task, "taken"))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(new RowStateModel()));
        var field = form.FindField("Name")!;

        var pending = state.ValidateFieldAsync(field, "Name");
        state.Reset();
        Assert.False(state.IsValidatingField("Name"));

        lookup.SetResult(false);
        await pending;

        // The verdict was about the data before the reset; it must not land on the cleared form.
        Assert.Empty(state.MessagesFor("Name"));
    }

    [Fact]
    public async Task A_cancelled_options_load_does_not_take_down_its_replacements_flag()
    {
        var first = new TaskCompletionSource<IReadOnlyList<BlazorFormSelectOption>>();
        var second = new TaskCompletionSource<IReadOnlyList<BlazorFormSelectOption>>();
        var calls = 0;
        var form = BlazorFormBuilder.For<RowStateModel>()
            .Field(x => x.Name, f => f.OptionsFrom(_ =>
                new ValueTask<IReadOnlyList<BlazorFormSelectOption>>(++calls == 1 ? first.Task : second.Task)))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(new RowStateModel()));
        var field = form.FindField("Name")!;

        var a = state.EnsureOptionsAsync(field, "Name");
        state.InvalidateOptions("Name");
        var b = state.EnsureOptionsAsync(field, "Name");

        first.SetResult([]);
        await a;

        // The second load is still running, and a third must not be started alongside it.
        Assert.True(state.IsLoadingOptions("Name"));
        await state.EnsureOptionsAsync(field, "Name");
        Assert.Equal(2, calls);

        second.SetResult([new BlazorFormSelectOption("a", "A")]);
        await b;
        Assert.False(state.IsLoadingOptions("Name"));
        Assert.Single(state.OptionsFor(field, "Name"));
    }
}

/// <summary>
/// Everything the form keeps per path has to follow its row when the rows move — and go with it when
/// it is removed.
/// </summary>
public class RowStateTests
{
    [Fact]
    public async Task A_rejected_entry_on_a_removed_row_does_not_block_submit()
    {
        var model = new RowStateModel { Rows = [new RowStateRow(), new RowStateRow()] };
        var form = BlazorFormBuilder.For<RowStateModel>()
            .Array(x => x.Rows, r => r.Field(x => x.Qty))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        state.SetValue("Rows[1].Qty", "abc");
        Assert.NotEmpty(state.MessagesFor("Rows[1].Qty"));

        state.RemoveArrayItem("Rows", 1);

        Assert.True(await state.ValidateAsync());
        Assert.Empty(state.AllMessages);
    }

    [Fact]
    public async Task A_rejected_entry_follows_its_row_when_an_earlier_row_is_removed()
    {
        var model = new RowStateModel { Rows = [new RowStateRow(), new RowStateRow()] };
        var form = BlazorFormBuilder.For<RowStateModel>()
            .Array(x => x.Rows, r => r.Field(x => x.Qty))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        state.SetValue("Rows[1].Qty", "abc");
        state.RemoveArrayItem("Rows", 0);

        Assert.False(await state.ValidateAsync());
        Assert.NotEmpty(state.MessagesFor("Rows[0].Qty"));
        Assert.Empty(state.MessagesFor("Rows[1].Qty"));
    }

    [Fact]
    public async Task Loaded_choices_follow_their_row_when_an_earlier_row_is_removed()
    {
        var model = new RowStateModel { Rows = [new RowStateRow { Country = "nl" }, new RowStateRow { Country = "be" }] };
        var loads = 0;
        var form = BlazorFormBuilder.For<RowStateModel>()
            .Array(x => x.Rows, row => row
                .Field(r => r.Country)
                .Field(r => r.City, f => f.OptionsFrom(ctx =>
                {
                    loads++;
                    return new ValueTask<IReadOnlyList<BlazorFormSelectOption>>(
                        [new BlazorFormSelectOption("x", (string)ctx.Value("Country")!)]);
                }, "Country")))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));
        var city = form.FindField("Rows")!.ItemTemplate!.Children.Single(c => c.Name == "City");

        await state.EnsureOptionsAsync(city, "Rows[0].City");
        await state.EnsureOptionsAsync(city, "Rows[1].City");
        state.RemoveArrayItem("Rows", 0);
        await state.EnsureOptionsAsync(city, "Rows[0].City");

        Assert.Equal("be", state.OptionsFor(city, "Rows[0].City").Single().Label);
        Assert.Empty(state.OptionsFor(city, "Rows[1].City"));
        // Moved, not reloaded.
        Assert.Equal(2, loads);
    }

    [Fact]
    public async Task An_options_provider_on_a_row_reads_that_rows_values()
    {
        var model = new RowStateModel { Rows = [new RowStateRow { Country = "nl" }, new RowStateRow { Country = "be" }] };
        var form = BlazorFormBuilder.For<RowStateModel>()
            .Array(x => x.Rows, row => row
                .Field(r => r.Country)
                .Field(r => r.City, f => f.OptionsFrom(ctx =>
                    new ValueTask<IReadOnlyList<BlazorFormSelectOption>>(
                        [new BlazorFormSelectOption("x", ctx.Value("Country") as string ?? "none")]), "Country")))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));
        var city = form.FindField("Rows")!.ItemTemplate!.Children.Single(c => c.Name == "City");

        await state.EnsureOptionsAsync(city, "Rows[1].City");

        Assert.Equal("be", state.OptionsFor(city, "Rows[1].City").Single().Label);
    }

    [Fact]
    public void An_array_property_can_be_added_to_removed_from_and_reordered()
    {
        var a = new RowStateRow { City = "a" };
        var b = new RowStateRow { City = "b" };
        var model = new RowStateModel { Fixed = [a, b] };
        var form = BlazorFormBuilder.For<RowStateModel>()
            .Array(x => x.Fixed, r => r.Field(x => x.City))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));
        var field = form.FindField("Fixed")!;

        state.MoveArrayItem("Fixed", 0, 1);
        Assert.Equal([b, a], model.Fixed);

        state.SwapArrayItems("Fixed", 0, 1);
        Assert.Equal([a, b], model.Fixed);

        state.RemoveArrayItem("Fixed", 0);
        Assert.Equal([b], model.Fixed);

        Assert.Equal(1, state.AddArrayItem(field, "Fixed"));
        Assert.Equal(2, model.Fixed.Length);

        state.ClearArrayItems("Fixed");
        Assert.Empty(model.Fixed);
    }

    [Fact]
    public void Reset_restores_an_array_property()
    {
        var model = new RowStateModel { Labels = ["one", "two"] };
        var form = BlazorFormBuilder.For<RowStateModel>()
            .Field(x => x.Labels, f => f.AsTags())
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        state.SetValue("Labels", new[] { "three" });
        state.Reset();

        Assert.Equal(["one", "two"], model.Labels);
        Assert.False(state.IsFormDirty);
    }

    [Fact]
    public void A_duplicated_row_does_not_share_the_originals_list()
    {
        var model = new RowStateModel { Rows = [new RowStateRow { Tags = ["x"] }] };
        var form = BlazorFormBuilder.For<RowStateModel>()
            .Array(x => x.Rows, r => r.Field(x => x.Tags, f => f.AsTags()))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        Assert.Equal(1, state.DuplicateArrayItem(form.FindField("Rows")!, "Rows", 0));
        model.Rows[1].Tags.Add("y");

        Assert.Equal(["x"], model.Rows[0].Tags);
    }
}

/// <summary>A value the model refused only matters while its control is on the page.</summary>
public class HiddenConversionErrorTests
{
    [Fact]
    public async Task A_rejected_entry_in_a_field_that_is_then_hidden_does_not_block_submit()
    {
        var form = BlazorFormBuilder.For<RowStateModel>()
            .Field(x => x.Show)
            .Field(x => x.Count, f => f.VisibleWhen("Show", BlazorFormConditionOperator.Equals, true))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(new RowStateModel()));

        state.SetValue("Count", "abc");
        Assert.NotEmpty(state.MessagesFor("Count"));

        state.SetValue("Show", false);

        Assert.Empty(state.MessagesFor("Count"));
        Assert.True(await state.ValidateAsync());
    }

    [Fact]
    public async Task A_rejected_entry_on_a_visible_field_still_blocks_submit()
    {
        var form = BlazorFormBuilder.For<RowStateModel>()
            .Field(x => x.Show)
            .Field(x => x.Count, f => f.VisibleWhen("Show", BlazorFormConditionOperator.Equals, true))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(new RowStateModel()));

        state.SetValue("Count", "abc");

        Assert.False(await state.ValidateAsync());
        Assert.NotEmpty(state.MessagesFor("Count"));
    }
}

/// <summary>
/// A failed submit on a wizard has to show the user what failed, even when the field at fault is on a
/// step other than the one they are looking at.
/// </summary>
public class WizardSubmitErrorStepTests : ComponentTestBase
{
    private static BlazorFormDefinition Schema() => BlazorFormBuilder.Create()
        .Checkbox("needsB")
        .Text("b", f => f.RequiredWhen("needsB", BlazorFormConditionOperator.IsTrue))
        .Text("c")
        .Step("one", s => s.Fields("needsB"))
        .Step("two", s => s.Fields("b"))
        .Step("three", s => s.Fields("c"))
        .Build();

    [Fact]
    public async Task Submit_moves_to_the_earliest_step_with_an_error()
    {
        var state = new BlazorFormState(Schema(), new BlazorFormDictionaryDataAccessor());
        await state.NextStepAsync();
        await state.NextStepAsync();
        // Back to step one to change an answer that makes step two incomplete, then straight to the end.
        state.GoToStep(0);
        state.SetValue("needsB", true);
        state.GoToStep(2);

        var cut = Render<BlazorFormView>(p => p.Add(x => x.State, state));
        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Equal(1, state.CurrentStepIndex);
        Assert.NotEmpty(state.MessagesFor("b"));
    }

    [Fact]
    public async Task Submit_stays_put_when_the_current_step_has_an_error_of_its_own()
    {
        var schema = BlazorFormBuilder.Create()
            .Checkbox("needsB")
            .Text("b", f => f.RequiredWhen("needsB", BlazorFormConditionOperator.IsTrue))
            .Text("c", f => f.Required())
            .Step("one", s => s.Fields("needsB"))
            .Step("two", s => s.Fields("b"))
            .Step("three", s => s.Fields("c"))
            .Build();
        var state = new BlazorFormState(schema, new BlazorFormDictionaryDataAccessor());
        await state.NextStepAsync();
        await state.NextStepAsync();
        state.GoToStep(0);
        state.SetValue("needsB", true);
        state.GoToStep(2);

        var cut = Render<BlazorFormView>(p => p.Add(x => x.State, state));
        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Equal(2, state.CurrentStepIndex);
    }
}

/// <summary>
/// A dictionary only has the keys someone wrote. A row or object that has not been typed into yet must
/// still own the fields its schema declares, or its conditions read the root's values instead.
/// </summary>
public class DictionaryRowScopeTests
{
    private static BlazorFormDefinition Schema() => BlazorFormBuilder.Create()
        .Text("Kind")
        .Array("items", item => item
            .Text("Kind")
            .Text("Detail", f => f.Required().VisibleWhen("Kind", BlazorFormConditionOperator.Equals, "x")))
        .Build();

    [Fact]
    public async Task A_new_row_answers_its_conditions_from_its_own_fields()
    {
        var form = Schema();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        state.SetValue("Kind", "x");

        state.AddArrayItem(form.FindField("items")!, "items");

        Assert.False(state.IsVisible(form.FindField("items")!.ItemTemplate!.Children.Single(c => c.Name == "Detail"), "items[0].Detail"));
        Assert.True(await state.ValidateAsync());
    }

    [Fact]
    public async Task A_row_supplied_without_a_key_owns_it_too()
    {
        var form = Schema();
        var data = new Dictionary<string, object?>
        {
            ["Kind"] = "x",
            ["items"] = new List<object?> { BlazorFormDictionaryDataAccessor.NewObject() }
        };
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor(data));

        Assert.True(await state.ValidateAsync());
    }
}

public class ExclusiveRangeTests
{
    private static async Task<BlazorFormRuleResult> Check(BlazorFormRangeRule rule, object? value)
        => await rule.ValidateAsync(new BlazorFormValidationContext("x", value, new BlazorFormDictionaryDataAccessor()));

    [Fact]
    public async Task An_exclusive_bound_rejects_the_bound_itself_and_says_so()
    {
        var rule = new BlazorFormRangeRule(0, 10) { MinExclusive = true, MaxExclusive = true };

        Assert.True((await Check(rule, 0.5)).IsValid);
        Assert.Equal("Must be greater than 0.", (await Check(rule, 0)).Message);
        Assert.Equal("Must be less than 10.", (await Check(rule, 10)).Message);
    }

    [Fact]
    public async Task An_inclusive_range_keeps_its_between_message()
    {
        var rule = new BlazorFormRangeRule(1, 5);

        Assert.True((await Check(rule, 5)).IsValid);
        Assert.Equal("Must be between 1 and 5.", (await Check(rule, 6)).Message);
    }

    [Fact]
    public async Task NaN_is_never_inside_a_range()
    {
        Assert.False((await Check(new BlazorFormRangeRule(0, 10), double.NaN)).IsValid);
    }
}

public class RequiredGroupDescriptionTests : ComponentTestBase
{
    [Theory]
    [InlineData(BlazorFormFieldType.MultiSelect)]
    [InlineData(BlazorFormFieldType.Tags)]
    public void A_required_group_is_described_as_required_rather_than_carrying_aria_required(BlazorFormFieldType type)
    {
        var form = BlazorFormBuilder.Create()
            .Field("days", type, f => f.Required().Options(("mon", "Monday"), ("tue", "Tuesday")).As(type))
            .Build();

        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, form));
        var group = cut.Find("#ff_days");

        Assert.False(group.HasAttribute("aria-required"));
        Assert.StartsWith("ff_days_required", group.GetAttribute("aria-describedby"));
        Assert.Equal("Required", cut.Find("#ff_days_required").TextContent);
    }

    [Fact]
    public void An_optional_group_has_no_required_note()
    {
        var form = BlazorFormBuilder.Create()
            .MultiSelect("days", f => f.Options(("mon", "Monday")))
            .Build();

        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, form));

        Assert.Empty(cut.FindAll("#ff_days_required"));
    }
}

public class TypedChoiceBuilderTests
{
    [Fact]
    public void An_integer_given_choices_stores_and_exports_integers()
    {
        var form = BlazorFormBuilder.Create()
            .Integer("seats", f => f.Options(("1", "One"), ("2", "Two")))
            .Build();
        var field = form.FindField("seats")!;

        Assert.Equal(BlazorFormFieldType.Select, field.Type);
        Assert.Equal(2L, BlazorFormValueConverter.FromInputString("2", field.ValueType, field.Type));

        var json = BlazorFormJsonSchemaExporter.Export(form, indented: false);
        Assert.Contains("\"enum\":[1,2]", json);
    }
}

public class ComponentReviewFollowUpTests : ComponentTestBase
{
    [Fact]
    public async Task A_provider_that_throws_its_own_cancellation_is_recorded_as_failed_not_retried_forever()
    {
        var calls = 0;
        var form = BlazorFormBuilder.Create()
            .Select("city", f => f.OptionsFrom(_ =>
            {
                calls++;
                throw new OperationCanceledException("timed out");
            }))
            .Build();

        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, form));
        await cut.InvokeAsync(() => cut.Instance.Form.SetValue("unrelated", 1));

        Assert.Equal(1, calls);
        Assert.NotNull(cut.Instance.Form.OptionsError("city"));
    }

    [Fact]
    public async Task The_error_summary_renders_without_a_navigation_manager()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBlazorForm();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new Microsoft.AspNetCore.Components.Web.HtmlRenderer(provider,
            provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>());

        var form = BlazorFormBuilder.Create().Text("name", f => f.Required()).Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        await state.SubmitAsync();

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<BlazorFormErrorSummary>(
                Microsoft.AspNetCore.Components.ParameterView.FromDictionary(new Dictionary<string, object?> { ["State"] = state }));
            return output.ToHtmlString();
        });

        Assert.Contains("href=\"#ff_name\"", html);
    }

    [Fact]
    public async Task A_read_only_confirmation_screen_still_submits_from_its_own_button()
    {
        var form = BlazorFormBuilder.Create().Text("name").Build();
        var submitted = false;
        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, form)
            .Add(x => x.ReadOnly, true)
            .Add(x => x.OnValidSubmit, (BlazorFormState _) => { submitted = true; })
            .Add(x => x.Footer, (Microsoft.AspNetCore.Components.RenderFragment)(b =>
            {
                b.OpenElement(0, "button");
                b.AddAttribute(1, "type", "submit");
                b.AddContent(2, "Confirm");
                b.CloseElement();
            })));

        await cut.Find("form").SubmitAsync();

        Assert.True(submitted);
    }

    [Fact]
    public async Task A_read_only_form_with_only_its_own_buttons_does_not_submit_on_enter()
    {
        var form = BlazorFormBuilder.Create().Text("name").Build();
        var submitted = false;
        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, form)
            .Add(x => x.ReadOnly, true)
            .Add(x => x.OnValidSubmit, (BlazorFormState _) => { submitted = true; }));

        await cut.Find("form").SubmitAsync();

        Assert.False(submitted);
    }
}
