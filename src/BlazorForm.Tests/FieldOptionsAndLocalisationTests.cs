using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorForm.Tests;

/// <summary>Renders every UI string in Klingon, so anything hard-coded in English stands out.</summary>
public sealed class StubMessageProvider : IBlazorFormMessageProvider
{
    public string Get(string key, params object?[] args) => $"[{key}]";
}

public class LocalisationTests : BunitContext
{
    public LocalisationTests() => Services.AddBlazorForm();

    private void UseStubMessages() => Services.AddBlazorFormMessages(new StubMessageProvider());

    [Fact]
    public void Every_button_and_placeholder_goes_through_the_message_provider()
    {
        UseStubMessages();
        var form = BlazorFormBuilder.Create()
            .Select("topic", f => f.Options(("a", "A")))
            .ArrayOf("tags", BlazorFormFieldType.Text)
            .Build();

        var html = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, form)
            .Add(x => x.ShowResetButton, true)).Markup;

        Assert.Contains($"[{BlazorFormMessageKeys.Submit}]", html, StringComparison.Ordinal);
        Assert.Contains($"[{BlazorFormMessageKeys.Reset}]", html, StringComparison.Ordinal);
        Assert.Contains($"[{BlazorFormMessageKeys.SelectPlaceholder}]", html, StringComparison.Ordinal);
        Assert.Contains($"[{BlazorFormMessageKeys.ArrayAdd}]", html, StringComparison.Ordinal);
        Assert.Contains($"[{BlazorFormMessageKeys.ArrayEmpty}]", html, StringComparison.Ordinal);

        // Nothing English survived.
        Assert.DoesNotContain("-- Select --", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Submit<", html, StringComparison.Ordinal);
    }

    [Fact]
    public void An_explicit_button_label_still_wins_over_the_provider()
    {
        UseStubMessages();
        var form = BlazorFormBuilder.Create().Text("name").Build();

        var html = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, form)
            .Add(x => x.SubmitText, "Send it")).Markup;

        Assert.Contains("Send it", html, StringComparison.Ordinal);
    }

    [Fact]
    public void The_wizard_announces_its_position_and_names_its_stepper()
    {
        UseStubMessages();
        var form = BlazorFormBuilder.Create()
            .Text("a").Text("b")
            .Step("one", s => s.Fields("a"))
            .Step("two", s => s.Fields("b"))
            .Build();

        var html = Render<BlazorFormView>(p => p.Add(x => x.Definition, form)).Markup;

        Assert.Contains($"aria-label=\"[{BlazorFormMessageKeys.Progress}]\"", html, StringComparison.Ordinal);
        Assert.Contains($"[{BlazorFormMessageKeys.StepOf}]", html, StringComparison.Ordinal);
        // The step body is focusable, so advancing can take the user to the new content.
        Assert.Contains("class=\"ff-step-body \" tabindex=\"-1\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void The_wizard_buttons_are_localised()
    {
        UseStubMessages();
        var form = BlazorFormBuilder.Create()
            .Text("a").Text("b")
            .Step("one", s => s.Fields("a"))
            .Step("two", s => s.Fields("b"))
            .Build();

        var html = Render<BlazorFormView>(p => p.Add(x => x.Definition, form)).Markup;

        Assert.Contains($"[{BlazorFormMessageKeys.Back}]", html, StringComparison.Ordinal);
        Assert.Contains($"[{BlazorFormMessageKeys.Next}]", html, StringComparison.Ordinal);
    }
}

public class FieldPresentationTests : BunitContext
{
    public FieldPresentationTests() => Services.AddBlazorForm();

    private string RenderMarkup(BlazorFormDefinition form)
        => Render<BlazorFormView>(p => p.Add(x => x.Definition, form)).Markup;

    [Fact]
    public void A_prefix_and_suffix_are_rendered_around_the_input()
    {
        var form = BlazorFormBuilder.Create()
            .Number("price", f => f.Label("Price").Prefix("$").Suffix("per month"))
            .Build();

        var html = RenderMarkup(form);

        Assert.Contains("ff-affix--prefix", html, StringComparison.Ordinal);
        Assert.Contains("ff-affix--suffix", html, StringComparison.Ordinal);
        // Decoration, not content: a screen reader reads the label, not the "$".
        Assert.Contains("aria-hidden=\"true\">$<", html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_hidden_label_becomes_the_accessible_name()
    {
        var form = BlazorFormBuilder.Create()
            .Text("q", f => f.Label("Search").HideLabel())
            .Build();

        var html = RenderMarkup(form);

        Assert.DoesNotContain("class=\"ff-label\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Search\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_radio_group_with_a_hidden_label_is_still_named()
    {
        var form = BlazorFormBuilder.Create()
            .Radio("size", f => f.Label("Size").HideLabel().Options(("s", "Small"), ("l", "Large")))
            .Build();

        var html = RenderMarkup(form);

        // Pointing aria-labelledby at an element that was never rendered would leave it anonymous.
        Assert.DoesNotContain("aria-labelledby", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Size\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_character_counter_reports_the_current_length()
    {
        var form = BlazorFormBuilder.Create()
            .TextArea("bio", f => f.MaxLength(200).CharacterCount())
            .Build();
        var data = new Dictionary<string, object?> { ["bio"] = "hello" };

        var html = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, form)
            .Add(x => x.Data, data)).Markup;

        Assert.Contains("ff-counter", html, StringComparison.Ordinal);
        Assert.Contains("5 / 200", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Schema_attributes_are_splatted_onto_the_control()
    {
        var form = BlazorFormBuilder.Create()
            .Text("code", f => f.InputAttr("data-testid", "code-box").InputAttr("spellcheck", "false"))
            .Build();

        var html = RenderMarkup(form);

        Assert.Contains("data-testid=\"code-box\"", html, StringComparison.Ordinal);
        Assert.Contains("spellcheck=\"false\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Splatted_attributes_cannot_overwrite_the_accessibility_wiring()
    {
        var form = BlazorFormBuilder.Create()
            .Text("name", f => f.Label("Name").Help("Hint").InputAttr("aria-describedby", "hijacked"))
            .Build();

        var html = RenderMarkup(form);

        Assert.Contains("aria-describedby=\"ff_name_help\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("hijacked", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Suggestions_render_a_datalist_without_restricting_the_field()
    {
        var form = BlazorFormBuilder.Create()
            .Text("city", f => f.Suggest("Berlin", "Cairo"))
            .Build();

        var html = RenderMarkup(form);

        Assert.Contains("<datalist id=\"ff_city_list\"", html, StringComparison.Ordinal);
        Assert.Contains("list=\"ff_city_list\"", html, StringComparison.Ordinal);
        Assert.Contains("Berlin", html, StringComparison.Ordinal);
        // Still a text box, not a select.
        Assert.Contains("type=\"text\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Static_content_renders_a_heading_and_no_input()
    {
        var form = BlazorFormBuilder.Create()
            .Static("intro", "Your details", "We only use these to contact you.")
            .Text("name")
            .Build();

        var html = RenderMarkup(form);

        Assert.Contains("role=\"heading\"", html, StringComparison.Ordinal);
        Assert.Contains("Your details", html, StringComparison.Ordinal);
        Assert.Contains("We only use these to contact you.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ff_intro\" class", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Static_content_is_never_validated_and_never_blocks_a_submit()
    {
        var form = BlazorFormBuilder.Create()
            .Static("intro", "Heading")
            .Text("name")
            .Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        Assert.True(await state.ValidateAsync());
    }

    [Fact]
    public void A_revealable_password_toggles_between_masked_and_plain()
    {
        var form = BlazorFormBuilder.Create()
            .Password("secret", f => f.Revealable())
            .Build();
        var view = Render<BlazorFormView>(p => p.Add(x => x.Definition, form));

        Assert.Contains("type=\"password\"", view.Markup, StringComparison.Ordinal);
        Assert.Contains("aria-pressed=\"false\"", view.Markup, StringComparison.Ordinal);

        view.Find("button[aria-pressed]").Click();

        Assert.Contains("type=\"text\"", view.Markup, StringComparison.Ordinal);
        Assert.Contains("aria-pressed=\"true\"", view.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void A_password_is_masked_unless_the_schema_opts_into_revealing_it()
    {
        // Whether a password may be shown at all depends on where the form runs, so it is never assumed.
        var form = BlazorFormBuilder.Create().Password("secret").Build();

        var html = RenderMarkup(form);

        Assert.Contains("type=\"password\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-pressed", html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_clear_button_appears_only_when_there_is_something_to_clear()
    {
        var form = BlazorFormBuilder.Create().Text("q", f => f.Clearable()).Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        var view = Render<BlazorFormView>(p => p.Add(x => x.State, state));

        Assert.Empty(view.FindAll("button.ff-affix--btn"));

        view.Find("input").Change("hello");
        Assert.Single(view.FindAll("button.ff-affix--btn"));

        view.Find("button.ff-affix--btn").Click();
        Assert.Null(state.GetValue("q"));
    }

    [Fact]
    public void A_switch_is_a_checkbox_that_announces_itself_as_one()
    {
        var form = BlazorFormBuilder.Create()
            .Checkbox("notify", f => f.Label("Email me").AsSwitch())
            .Build();

        var html = RenderMarkup(form);

        Assert.Contains("type=\"checkbox\"", html, StringComparison.Ordinal);
        Assert.Contains("role=\"switch\"", html, StringComparison.Ordinal);
        Assert.Contains("ff-switch", html, StringComparison.Ordinal);
    }
}

public class SchemaDiagnosticsTests
{
    [Fact]
    public void A_healthy_schema_reports_nothing()
    {
        var form = BlazorFormBuilder.For<RegistrationModel>()
            .Field(x => x.FirstName)
            .Field(x => x.Email)
            .Build();

        Assert.Empty(form.Validate());
    }

    [Fact]
    public void Duplicate_sibling_names_are_reported()
    {
        // The builder rejects duplicates, so this is the schema-assembled-by-hand case.
        var form = new BlazorFormDefinition();
        form.Fields.Add(new BlazorFormFieldDefinition("name", BlazorFormFieldType.Text));
        form.Fields.Add(new BlazorFormFieldDefinition("Name", BlazorFormFieldType.Text));

        var problem = Assert.Single(form.Validate());
        Assert.Equal(BlazorFormSchemaDiagnosticSeverity.Error, problem.Severity);
        Assert.Contains("overwrite each other", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_array_without_an_item_template_is_reported()
    {
        var form = new BlazorFormDefinition();
        form.Fields.Add(new BlazorFormFieldDefinition("lines", BlazorFormFieldType.Array));

        var problem = Assert.Single(form.Validate());
        Assert.Contains("ItemTemplate", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_step_naming_an_unknown_field_is_reported()
    {
        var form = BlazorFormBuilder.Create()
            .Text("name")
            .Step("one", s => s.Fields("name", "nope"))
            .Build();

        var problem = Assert.Single(form.Validate(), d => d.Path == "nope");
        Assert.Contains("not in the schema", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_condition_pointing_at_no_field_is_a_warning()
    {
        var form = BlazorFormBuilder.Create()
            .Text("name", f => f.VisibleWhen("ghost", BlazorFormConditionOperator.IsTrue))
            .Build();

        var problem = Assert.Single(form.Validate());
        Assert.Equal(BlazorFormSchemaDiagnosticSeverity.Warning, problem.Severity);
        Assert.Contains("'ghost'", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_condition_naming_a_sibling_inside_a_repeater_is_accepted()
    {
        var form = BlazorFormBuilder.For<ContactBook>()
            .Field(x => x.Kind)
            .Array(x => x.Rows, row => row
                .Field(r => r.Kind)
                .Field(r => r.Email, f => f.VisibleWhen("Kind", BlazorFormConditionOperator.Equals, "email")))
            .Build();

        Assert.Empty(form.Validate());
    }

    [Fact]
    public void A_field_belonging_to_no_wizard_step_is_a_warning()
    {
        var form = BlazorFormBuilder.Create()
            .Text("shown").Text("orphan")
            .Step("one", s => s.Fields("shown"))
            .Build();

        var problem = Assert.Single(form.Validate());
        Assert.Equal("orphan", problem.Path);
        Assert.Equal(BlazorFormSchemaDiagnosticSeverity.Warning, problem.Severity);
    }
}
