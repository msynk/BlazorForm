using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorForm.Tests;

/// <summary>
/// Which DOM event writes the value back. The default (<c>change</c>) fires on blur for a text box, so
/// a form that wants a live preview or an as-you-type total has to opt into <c>input</c>.
/// </summary>
public class UpdateTriggerTests : BunitContext
{
    public UpdateTriggerTests() => Services.AddBlazorForm();

    private (Bunit.IRenderedComponent<BlazorFormView> View, BlazorFormState State) RenderForm(
        BlazorFormDefinition form)
    {
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        var view = Render<BlazorFormView>(p => p.Add(x => x.State, state));
        return (view, state);
    }

    [Fact]
    public void By_default_a_field_writes_on_change_and_wires_no_input_handler_at_all()
    {
        var form = BlazorFormBuilder.Create().Text("name").Build();
        var (view, state) = RenderForm(form);

        // Not merely ignored — never wired. On a Blazor Server circuit an `oninput` attribute costs a
        // round-trip per keystroke, so a field with no use for the event must not carry one.
        Assert.Throws<Bunit.MissingEventHandlerException>(() => view.Find("input").Input("Ada"));

        view.Find("input").Change("Ada");
        Assert.Equal("Ada", state.GetValue("name"));
    }

    [Fact]
    public void A_change_driven_field_with_a_counter_listens_to_input_without_writing()
    {
        var form = BlazorFormBuilder.Create()
            .Text("name", f => f.MaxLength(10).CharacterCount())
            .Build();
        var (view, state) = RenderForm(form);

        view.Find("input").Input("Ada");

        // The counter tracks what is on screen; the model still waits for the change event.
        Assert.Contains("3 / 10", view.Markup, StringComparison.Ordinal);
        Assert.Null(state.GetValue("name"));
    }

    [Fact]
    public void An_input_driven_field_writes_on_every_keystroke()
    {
        var form = BlazorFormBuilder.Create().Text("name", f => f.UpdateOnInput()).Build();
        var (view, state) = RenderForm(form);

        view.Find("input").Input("A");
        Assert.Equal("A", state.GetValue("name"));

        view.Find("input").Input("Ad");
        Assert.Equal("Ad", state.GetValue("name"));
    }

    [Fact]
    public void An_input_driven_field_ignores_the_change_event_so_it_never_writes_twice()
    {
        var writes = 0;
        var form = BlazorFormBuilder.Create().Text("name", f => f.UpdateOnInput()).Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        var view = Render<BlazorFormView>(p => p
            .Add(x => x.State, state)
            .Add(x => x.OnFieldChanged, _ => writes++));

        view.Find("input").Input("Ada");
        view.Find("input").Change("Ada");

        Assert.Equal(1, writes);
    }

    [Fact]
    public async Task A_debounced_field_writes_once_the_typing_stops()
    {
        var form = BlazorFormBuilder.Create().Text("q", f => f.UpdateOnInput(debounceMilliseconds: 60)).Build();
        var (view, state) = RenderForm(form);

        view.Find("input").Input("a");
        view.Find("input").Input("ab");
        view.Find("input").Input("abc");

        // Nothing has landed yet: every keystroke superseded the one before it.
        Assert.Null(state.GetValue("q"));

        await Task.Delay(300);
        Assert.Equal("abc", state.GetValue("q"));
    }

    [Fact]
    public void A_slider_always_writes_live_whatever_the_trigger_says()
    {
        // Dragging *is* the interaction; waiting for `change` would leave the readout a step behind.
        var form = BlazorFormBuilder.Create()
            .Field("volume", BlazorFormFieldType.Range, f => f.Range(0, 10))
            .Build();
        var (view, state) = RenderForm(form);

        view.Find("input[type=range]").Input("7");

        Assert.Equal(7d, state.GetValue("volume"));
    }
}

public class FocusTests : BunitContext
{
    public FocusTests() => Services.AddBlazorForm();

    [Fact]
    public async Task A_focus_target_is_registered_for_every_rendered_field()
    {
        var form = BlazorFormBuilder.Create().Text("name").Text("email").Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        Render<BlazorFormView>(p => p.Add(x => x.State, state));

        // bUnit has no real DOM focus, so the contract under test is the registration, not the caret:
        // a path the form is rendering resolves, one it is not does not.
        Assert.True(await state.FocusAsync("name"));
        Assert.False(await state.FocusAsync("nope"));
    }

    [Fact]
    public async Task A_hidden_field_registers_no_focus_target()
    {
        var form = BlazorFormBuilder.Create()
            .Checkbox("advanced")
            .Text("detail", f => f.VisibleWhen("advanced", BlazorFormConditionOperator.IsTrue))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        Render<BlazorFormView>(p => p.Add(x => x.State, state));

        Assert.False(await state.FocusAsync("detail"));
    }

    [Fact]
    public async Task A_control_with_no_focusable_element_reports_that_it_could_not_take_focus()
    {
        // Reporting success while doing nothing would make "focus the first error" stop at the first
        // field it cannot reach, instead of moving on to one it can.
        var form = BlazorFormBuilder.Create().File("cv").Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        Render<BlazorFormView>(p => p.Add(x => x.State, state));

        Assert.False(await state.FocusAsync("cv"));
    }

    [Fact]
    public void A_radio_group_offers_the_group_itself_as_the_focus_target()
    {
        // No single radio represents the field, and landing on the group makes a screen reader announce
        // its name and role before the user arrows through the options.
        var form = BlazorFormBuilder.Create()
            .Radio("size", f => f.Options(("s", "Small"), ("l", "Large")))
            .Build();

        var html = Render<BlazorFormView>(p => p.Add(x => x.Definition, form)).Markup;

        Assert.Contains("role=\"radiogroup\"", html, StringComparison.Ordinal);
        Assert.Contains("tabindex=\"-1\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void The_autofocus_attribute_is_no_longer_emitted()
    {
        // Browsers honour it only on the initial page load, which an interactive Blazor render is not;
        // leaving it in the markup would suggest a behaviour that never actually happens.
        var form = BlazorFormBuilder.Create().Text("name", f => f.Autofocus()).Build();

        var html = Render<BlazorFormView>(p => p.Add(x => x.Definition, form)).Markup;

        Assert.DoesNotContain("autofocus", html, StringComparison.OrdinalIgnoreCase);
    }
}
