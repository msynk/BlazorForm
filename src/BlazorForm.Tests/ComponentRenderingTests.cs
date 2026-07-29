using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorForm.Tests;

/// <summary>
/// Renders the components for real, so the parts a unit test on the schema cannot see — the emitted
/// HTML, the accessibility wiring, and whether a field re-renders at all — are covered too.
/// </summary>
public abstract class ComponentTestBase : BunitContext
{
    protected ComponentTestBase()
    {
        Services.AddBlazorForm();
    }
}

public class FieldRenderingTests : ComponentTestBase
{
    private Bunit.IRenderedComponent<BlazorFormView> RenderForm(BlazorFormDefinition form, object? model = null)
        => model is null
            ? Render<BlazorFormView>(p => p.Add(x => x.Definition, form))
            : Render<BlazorFormView>(p => p.Add(x => x.Definition, form).Add(x => x.Model, model));

    [Fact]
    public void A_text_field_is_labelled_and_wired_for_assistive_tech()
    {
        var form = BlazorFormBuilder.Create()
            .Text("email", f => f.Label("Email").Required().Help("We never share it.").Autocomplete("email"))
            .Build();

        var cut = RenderForm(form);
        var input = cut.Find("input#ff_email");
        var label = cut.Find("label[for='ff_email']");

        Assert.Contains("Email", label.TextContent, StringComparison.Ordinal);
        Assert.Equal("true", input.GetAttribute("aria-required"));
        Assert.Equal("email", input.GetAttribute("autocomplete"));
        // The help text is announced with the field rather than being orphaned next to it.
        Assert.Equal("ff_email_help", input.GetAttribute("aria-describedby"));
        Assert.NotNull(cut.Find("#ff_email_help"));
    }

    [Fact]
    public void The_form_opts_out_of_native_browser_validation()
    {
        // BlazorForm owns validation; leaving the browser's on would let `pattern` block submit with a
        // bubble the app cannot style, translate or read.
        var cut = RenderForm(BlazorFormBuilder.Create().Text("a").Build());
        Assert.True(cut.Find("form").HasAttribute("novalidate"));
    }

    [Fact]
    public void A_read_only_field_is_readonly_not_disabled()
    {
        var cut = RenderForm(BlazorFormBuilder.Create().Text("a", f => f.ReadOnly()).Build());
        var input = cut.Find("input#ff_a");

        Assert.True(input.HasAttribute("readonly"));
        Assert.False(input.HasAttribute("disabled"));
    }

    [Fact]
    public void A_conditionally_disabled_field_is_disabled()
    {
        var form = BlazorFormBuilder.Create()
            .Checkbox("locked")
            .Text("a", f => f.DisabledWhen("locked", BlazorFormConditionOperator.IsTrue))
            .Build();

        var data = new Dictionary<string, object?> { ["locked"] = true };
        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, form).Add(x => x.Data, data));

        Assert.True(cut.Find("input#ff_a").HasAttribute("disabled"));
    }

    [Fact]
    public void Numeric_attributes_are_written_invariantly()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            var form = BlazorFormBuilder.Create().Number("price", f => f.Range(0.5, 10.5).Step(0.5)).Build();

            var input = RenderForm(form).Find("input#ff_price");

            // "0,5" would be an invalid HTML attribute value.
            Assert.Equal("0.5", input.GetAttribute("min"));
            Assert.Equal("10.5", input.GetAttribute("max"));
            Assert.Equal("0.5", input.GetAttribute("step"));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void A_radio_group_is_labelled_as_a_group()
    {
        var form = BlazorFormBuilder.Create()
            .Radio("kind", f => f.Label("Account type").Options(("a", "A"), ("b", "B")).Required())
            .Build();

        var cut = RenderForm(form);
        var group = cut.Find("[role='radiogroup']");

        // There is no single control for a <label for> to point at, so the group names itself.
        Assert.Equal("ff_kind_label", group.GetAttribute("aria-labelledby"));
        Assert.NotNull(cut.Find("#ff_kind_label"));
        Assert.Equal(2, cut.FindAll("input[type=radio]").Count);
    }

    [Fact]
    public void A_hidden_field_renders_nothing_visible()
    {
        var form = BlazorFormBuilder.Create()
            .Text("shown")
            .Text("secret", f => f.VisibleWhen("shown", BlazorFormConditionOperator.IsNotEmpty))
            .Build();

        var cut = RenderForm(form);

        Assert.NotNull(cut.Find("input#ff_shown"));
        Assert.Empty(cut.FindAll("input#ff_secret"));
    }

    [Fact]
    public void A_file_field_renders_a_file_input()
    {
        var form = BlazorFormBuilder.Create()
            .File("cv", f => f.Accept(".pdf", multiple: true))
            .Build();

        var input = RenderForm(form).Find("input[type=file]");

        Assert.Equal(".pdf", input.GetAttribute("accept"));
        Assert.True(input.HasAttribute("multiple"));
    }

    [Fact]
    public void An_unregistered_custom_renderer_fails_with_a_message_naming_the_key()
    {
        var form = BlazorFormBuilder.Create()
            .Field("rating", BlazorFormFieldType.Custom, f => f.CustomRenderer("stars"))
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() => RenderForm(form));
        Assert.Contains("stars", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Multi_column_layout_emits_a_grid()
    {
        var form = BlazorFormBuilder.Create()
            .Columns(2)
            .Text("a")
            .Text("b", f => f.ColumnSpan(2))
            .Build();

        var cut = RenderForm(form);

        Assert.Contains("ff-grid", cut.Find(".ff-form__body").ClassName, StringComparison.Ordinal);
        Assert.Contains("span 2", cut.Find(".ff-field:last-child").GetAttribute("style")!, StringComparison.Ordinal);
    }
}

public class ValidationRenderingTests : ComponentTestBase
{
    [Fact]
    public async Task Errors_appear_only_after_the_user_engages_with_the_form()
    {
        var form = BlazorFormBuilder.Create().Text("name", f => f.Required()).Build();
        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, form));

        // A blank form does not open covered in red.
        Assert.Empty(cut.FindAll(".ff-message"));

        await cut.Find("form").SubmitAsync();

        var message = cut.Find(".ff-message--error");
        Assert.Equal("This field is required.", message.TextContent);
        Assert.Equal("true", cut.Find("input#ff_name").GetAttribute("aria-invalid"));
        // The error is announced with the field, not left as an unassociated paragraph.
        Assert.Contains("ff_name_error", cut.Find("input#ff_name").GetAttribute("aria-describedby")!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_error_summary_lists_problems_in_schema_order_and_links_to_each_field()
    {
        var form = BlazorFormBuilder.Create()
            .Text("first", f => f.Label("First").Required())
            .Text("second", f => f.Label("Second").Required())
            .Build();

        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, form)
            .Add(x => x.ShowErrorSummary, true));

        Assert.Empty(cut.FindAll(".ff-summary"));

        await cut.Find("form").SubmitAsync();

        var links = cut.FindAll(".ff-summary__list a");
        Assert.Equal(2, links.Count);
        Assert.Equal("#ff_first", links[0].GetAttribute("href"));
        Assert.Contains("First:", links[0].TextContent, StringComparison.Ordinal);
        Assert.Equal("#ff_second", links[1].GetAttribute("href"));
        Assert.Equal("alert", cut.Find(".ff-summary").GetAttribute("role"));
    }

    [Fact]
    public async Task Form_level_messages_are_shown_even_without_a_summary()
    {
        var form = BlazorFormBuilder.For<BookingModel>()
            .Field(x => x.Start)
            .Field(x => x.End)
            .MustAll(m => m.End >= m.Start, "End must be on or after start.")
            .Build();

        var model = new BookingModel { Start = new DateOnly(2024, 5, 10), End = new DateOnly(2024, 5, 1) };
        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, form)
            .Add(x => x.Model, model));

        await cut.Find("form").SubmitAsync();

        Assert.Contains(cut.FindAll(".ff-message"), e => e.TextContent == "End must be on or after start.");
    }

    [Fact]
    public async Task A_valid_submit_reaches_the_handler_exactly_once()
    {
        var form = BlazorFormBuilder.Create().Text("name").Build();
        var calls = 0;

        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, form)
            .Add(x => x.OnValidSubmit, (BlazorFormState _) => calls++));

        await cut.Find("form").SubmitAsync();

        Assert.Equal(1, calls);
    }
}

public class ArrayRenderingTests : ComponentTestBase
{
    private static BlazorFormDefinition Invoice() => BlazorFormBuilder.For<RegistrationModel>()
        .Array(x => x.Items, i => i.Field(l => l.Product).Field(l => l.Quantity),
            f => f.Items(min: 1, max: 2).Attr("itemNoun", "line"))
        .Build();

    [Fact]
    public void An_empty_array_explains_itself_and_offers_an_add_button()
    {
        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, Invoice())
            .Add(x => x.Model, new RegistrationModel()));

        Assert.Equal("No lines yet.", cut.Find(".ff-array__empty").TextContent);
        Assert.Contains("Add line", cut.Find(".ff-btn--add").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Adding_and_removing_items_updates_the_model_and_the_dom()
    {
        var model = new RegistrationModel();
        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, Invoice())
            .Add(x => x.Model, model));

        cut.Find(".ff-btn--add").Click();

        Assert.Single(model.Items);
        Assert.NotNull(cut.Find("input#ff_Items_0_Product"));

        // At the minimum of one item, removal is refused rather than silently breaking the rule.
        Assert.True(cut.Find(".ff-btn--danger").HasAttribute("disabled"));

        cut.Find(".ff-btn--add").Click();
        Assert.Equal(2, model.Items.Count);

        // And at the maximum of two, no more can be added.
        Assert.True(cut.Find(".ff-btn--add").HasAttribute("disabled"));

        cut.FindAll(".ff-btn--danger")[0].Click();
        Assert.Single(model.Items);
    }

    [Fact]
    public void Reorder_buttons_are_labelled_for_screen_readers()
    {
        var model = new RegistrationModel();
        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, Invoice())
            .Add(x => x.Model, model));

        cut.Find(".ff-btn--add").Click();
        cut.Find(".ff-btn--add").Click();

        var buttons = cut.FindAll(".ff-btn--icon");
        Assert.Equal("Move line 1 up", buttons[0].GetAttribute("aria-label"));
        Assert.Equal("Move line 1 down", buttons[1].GetAttribute("aria-label"));
    }

    [Fact]
    public void Moving_an_item_carries_its_values_with_it()
    {
        var model = new RegistrationModel();
        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, Invoice())
            .Add(x => x.Model, model));

        cut.Find(".ff-btn--add").Click();
        cut.Find(".ff-btn--add").Click();
        cut.Find("input#ff_Items_0_Product").Change("first");
        cut.Find("input#ff_Items_1_Product").Change("second");

        // "Move down" on the first row.
        cut.FindAll(".ff-btn--icon")[1].Click();

        Assert.Equal("second", model.Items[0].Product);
        Assert.Equal("first", model.Items[1].Product);
    }
}

public class RenderEfficiencyTests : ComponentTestBase
{
    /// <summary>
    /// Counts how often it is rendered, so a test can prove a field was left alone. The counter is
    /// carried in a DI-registered object rather than a static so the tests stay independent.
    /// </summary>
    private sealed class RenderCounter
    {
        private readonly Dictionary<string, int> _byPath = new(StringComparer.Ordinal);

        public void Record(string path) => _byPath[path] = _byPath.GetValueOrDefault(path) + 1;

        public int this[string path] => _byPath.GetValueOrDefault(path);
    }

    private sealed class CountingInput : BlazorFormInputBase
    {
        [Microsoft.AspNetCore.Components.Inject] private RenderCounter Counter { get; set; } = default!;

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            Counter.Record(Context.Path);
            builder.OpenElement(0, "input");
            builder.AddAttribute(1, "id", Context.ElementId);
            builder.AddAttribute(2, "value", Context.StringValue);
            builder.AddAttribute(3, "onchange",
                Microsoft.AspNetCore.Components.EventCallback.Factory.Create<Microsoft.AspNetCore.Components.ChangeEventArgs>(
                    this, e => Context.SetFromStringAsync(e.Value?.ToString())));
            builder.CloseElement();
        }
    }

    [Fact]
    public void Editing_one_field_does_not_re_render_the_others()
    {
        // Without per-field render gating, every keystroke re-diffs every control on the form — which
        // is what makes large schema-driven forms feel slow.
        var counter = new RenderCounter();
        Services.AddSingleton(counter);
        // Registered before the base fixture's registry would win: AddBlazorForm uses TryAdd.
        Services.AddSingleton<IBlazorFormFieldRendererRegistry>(_ =>
        {
            var registry = new BlazorFormFieldRendererRegistry();
            registry.Register(BlazorFormFieldType.Text, typeof(CountingInput));
            return registry;
        });

        var form = BlazorFormBuilder.Create()
            .Text("a").Text("b").Text("c").Text("d").Text("e")
            .Build();

        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, form));

        Assert.Equal(1, counter["a"]);
        Assert.Equal(1, counter["e"]);

        cut.Find("input#ff_a").Change("hello");

        // The edited field redraws; every other field is left exactly as it was.
        Assert.True(counter["a"] > 1, "the edited field should re-render");
        foreach (var untouched in (string[])["b", "c", "d", "e"])
            Assert.Equal(1, counter[untouched]);
    }

    [Fact]
    public void A_field_whose_visibility_flips_does_re_render()
    {
        var form = BlazorFormBuilder.Create()
            .Text("trigger")
            .Text("dependent", f => f.VisibleWhen("trigger", BlazorFormConditionOperator.IsNotEmpty))
            .Build();

        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, form));
        Assert.Empty(cut.FindAll("input#ff_dependent"));

        cut.Find("input#ff_trigger").Change("something");

        Assert.NotNull(cut.Find("input#ff_dependent"));
    }
}

public class WizardRenderingTests : ComponentTestBase
{
    private static BlazorFormDefinition Wizard() => BlazorFormBuilder.For<SignupModel>()
        .Field(x => x.Email, f => f.Required())
        .Field(x => x.IsBusiness)
        .Field(x => x.CompanyName)
        .Field(x => x.Country)
        .Step("contact", s => s.Title("Contact").Fields("Email"))
        .Step("company", s => s.Title("Company").Fields("CompanyName")
            .VisibleWhen(nameof(SignupModel.IsBusiness), BlazorFormConditionOperator.IsTrue))
        .Step("done", s => s.Title("Done").Fields("Country"))
        .Build();

    [Fact]
    public void Hidden_steps_leave_no_gap_in_the_numbering()
    {
        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, Wizard())
            .Add(x => x.Model, new SignupModel()));

        var numbers = cut.FindAll(".ff-stepper__num").Select(n => n.TextContent).ToList();

        // The company step is hidden, so the remaining two are 1 and 2 — not 1 and 3.
        Assert.Equal(["1", "2"], numbers);
    }

    [Fact]
    public void A_step_will_not_advance_while_its_own_fields_are_invalid()
    {
        var model = new SignupModel();
        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, Wizard())
            .Add(x => x.Model, model));

        cut.Find(".ff-btn--primary").Click();
        Assert.Contains("Contact", cut.Find(".ff-stepper__step.is-active").TextContent, StringComparison.Ordinal);
        Assert.NotEmpty(cut.FindAll(".ff-message--error"));

        cut.Find("input#ff_Email").Change("ada@example.com");
        cut.Find(".ff-btn--primary").Click();

        Assert.Contains("Done", cut.Find(".ff-stepper__step.is-active").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Completed_steps_are_clickable_and_upcoming_ones_are_not()
    {
        var model = new SignupModel { Email = "ada@example.com" };
        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, Wizard())
            .Add(x => x.Model, model));

        Assert.Empty(cut.FindAll(".ff-stepper__link"));

        cut.Find(".ff-btn--primary").Click(); // advance to "Done"

        var links = cut.FindAll(".ff-stepper__link");
        Assert.Single(links);

        links[0].Click();
        Assert.Contains("Contact", cut.Find(".ff-stepper__step.is-active").TextContent, StringComparison.Ordinal);
    }
}

public class ReadOnlyModeTests : ComponentTestBase
{
    [Fact]
    public void Read_only_mode_locks_every_control_and_hides_the_buttons()
    {
        var form = BlazorFormBuilder.Create()
            .Text("name")
            .Select("kind", f => f.Options(("a", "A")))
            .Checkbox("agree")
            .Build();

        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, form)
            .Add(x => x.ReadOnly, true)
            .Add(x => x.ShowResetButton, true));

        Assert.True(cut.Find("input#ff_name").HasAttribute("readonly"));
        // A <select> and a checkbox have no readonly attribute, so they have to be disabled instead.
        Assert.True(cut.Find("select#ff_kind").HasAttribute("disabled"));
        Assert.True(cut.Find("input#ff_agree").HasAttribute("disabled"));
        Assert.Empty(cut.FindAll(".ff-form__actions"));
    }
}
