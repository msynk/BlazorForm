using Bunit;

namespace BlazorForm.Tests;

/// <summary>
/// A combobox holds a label and the model holds the value the label stands for. Half of a label
/// stands for nothing, so writing on every keystroke would store "Fran" as the country — and then
/// flag it as an answer that does not exist.
/// </summary>
public class ComboboxUpdateTriggerTests : ComponentTestBase
{
    private static BlazorFormDefinition Schema() => BlazorFormBuilder.Create()
        .Combobox("country", f => f
            .Options(("fr", "France"), ("gb", "United Kingdom"))
            .AsCombobox()
            .UpdateOnInput())
        .Build();

    [Fact]
    public void A_combobox_does_not_wire_the_input_event_even_when_asked_to()
    {
        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, Schema()));

        // Blazor omits an event attribute whose callback has no delegate, which is the whole point:
        // no handler, no keystroke-by-keystroke write of a partial label.
        Assert.DoesNotContain("oninput", cut.Find("input#ff_country").OuterHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void It_still_commits_when_the_choice_is_made()
    {
        var state = new BlazorFormState(Schema(), new BlazorFormDictionaryDataAccessor());
        var cut = Render<BlazorFormView>(p => p.Add(x => x.State, state));

        cut.Find("input#ff_country").Change("France");

        Assert.Equal("fr", state.GetValue("country"));
    }

    [Fact]
    public void A_plain_text_field_still_writes_as_the_user_types()
    {
        var form = BlazorFormBuilder.Create().Text("a", f => f.UpdateOnInput()).Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        var cut = Render<BlazorFormView>(p => p.Add(x => x.State, state));

        cut.Find("input#ff_a").Input("typing");

        Assert.Equal("typing", state.GetValue("a"));
    }
}

/// <summary>
/// A setting that is accepted and then ignored is the kind of thing that costs an afternoon. The
/// schema check reports it instead.
/// </summary>
public class UpdateTriggerDiagnosticTests
{
    private static IReadOnlyList<BlazorFormSchemaDiagnostic> Check(BlazorFormFieldType type)
        => BlazorFormBuilder.Create()
            .Field("x", type, f => f.Options(("a", "A")).As(type).UpdateOnInput())
            .Build()
            .Validate();

    [Theory]
    [InlineData(BlazorFormFieldType.Select)]
    [InlineData(BlazorFormFieldType.Radio)]
    [InlineData(BlazorFormFieldType.MultiSelect)]
    [InlineData(BlazorFormFieldType.Combobox)]
    public void A_control_that_only_ever_commits_reports_the_setting(BlazorFormFieldType type)
        => Assert.Contains(Check(type), d => d.Message.Contains("UpdateOnInput has no effect", StringComparison.Ordinal));

    [Theory]
    [InlineData(BlazorFormFieldType.Text)]
    [InlineData(BlazorFormFieldType.TextArea)]
    [InlineData(BlazorFormFieldType.Number)]
    [InlineData(BlazorFormFieldType.Range)]
    public void A_control_the_user_types_into_is_not_reported(BlazorFormFieldType type)
    {
        var form = BlazorFormBuilder.Create().Field("x", type, f => f.UpdateOnInput()).Build();

        Assert.DoesNotContain(form.Validate(), d => d.Message.Contains("UpdateOnInput", StringComparison.Ordinal));
    }

    [Fact]
    public void The_default_trigger_is_never_reported()
    {
        var form = BlazorFormBuilder.Create().Select("x", f => f.Options(("a", "A"))).Build();

        Assert.DoesNotContain(form.Validate(), d => d.Message.Contains("UpdateOnInput", StringComparison.Ordinal));
    }

    [Fact]
    public void It_is_a_warning_not_an_error_because_the_form_still_works()
    {
        var diagnostic = Check(BlazorFormFieldType.Select)
            .First(d => d.Message.Contains("UpdateOnInput", StringComparison.Ordinal));

        Assert.Equal(BlazorFormSchemaDiagnosticSeverity.Warning, diagnostic.Severity);
    }
}
