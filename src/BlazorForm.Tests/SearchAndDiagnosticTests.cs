using Bunit;

namespace BlazorForm.Tests;

public class SearchFieldTests : ComponentTestBase
{
    [Fact]
    public void A_search_field_renders_as_a_search_box()
    {
        // Browsers give type=search a clear affordance, a search key on the on-screen keyboard and
        // history from previous searches; none of that comes with a plain text box.
        var form = BlazorFormBuilder.Create().Text("q", f => f.AsSearch().HideLabel().Label("Search")).Build();

        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, form));
        var input = cut.Find("input#ff_q");

        Assert.Equal("search", input.GetAttribute("type"));
        Assert.Equal("search", input.GetAttribute("inputmode"));
        // The label was suppressed, so the control still has to be named for assistive technology.
        Assert.Equal("Search", input.GetAttribute("aria-label"));
    }

    [Fact]
    public void An_explicit_input_mode_is_not_overwritten()
    {
        var form = BlazorFormBuilder.Create().Text("q", f => f.InputMode("text").AsSearch()).Build();

        Assert.Equal("text", form.FindField("q")!.InputMode);
    }

    [Fact]
    public void It_survives_a_JSON_round_trip()
    {
        var form = BlazorFormBuilder.Create().Text("q", f => f.AsSearch()).Build();

        var reimported = BlazorFormJsonSchemaImporter.Import(BlazorFormJsonSchemaExporter.Export(form));

        Assert.Equal(BlazorFormFieldType.Search, reimported.FindField("q")!.Type);
    }

    [Fact]
    public void Adding_it_did_not_move_any_existing_field_type()
    {
        // The member is appended rather than slotted in beside Text, so nothing persisted as a number
        // changes meaning.
        Assert.Equal(0, (int)BlazorFormFieldType.Text);
        Assert.Equal(1, (int)BlazorFormFieldType.TextArea);
        Assert.Equal(22, (int)BlazorFormFieldType.Custom);
    }
}

public class CrossFieldRuleDiagnosticTests
{
    [Fact]
    public void A_confirm_rule_pointing_at_nothing_is_reported()
    {
        // The other value reads as null, this one is compared against it, and the field passes for
        // entirely the wrong reason — which is the hardest kind of bug to see in a form.
        var form = BlazorFormBuilder.Create()
            .Password("password")
            .Password("confirm", f => f.MatchesField("passwrod"))
            .Build();

        Assert.Contains(form.Validate(),
            d => d.Message.Contains("MatchesField refers to 'passwrod'", StringComparison.Ordinal));
    }

    [Fact]
    public void A_correct_one_is_not()
    {
        var form = BlazorFormBuilder.Create()
            .Password("password")
            .Password("confirm", f => f.MatchesField("password"))
            .Build();

        Assert.Empty(form.Validate());
    }

    [Fact]
    public void A_sibling_reference_inside_a_repeater_resolves()
    {
        var form = BlazorFormBuilder.Create()
            .Array("logins", row => row
                .Password("password")
                .Password("confirm", f => f.MatchesField("password")))
            .Build();

        Assert.Empty(form.Validate());
    }
}
