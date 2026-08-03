using Bunit;

namespace BlazorForm.Tests;

/// <summary>
/// The visible character counter is <c>aria-hidden</c>, so without a live region a screen-reader user
/// meets the limit by having their typing silently stop working.
/// </summary>
public class CharacterCountAnnouncementTests : ComponentTestBase
{
    private static BlazorFormDefinition Schema(int max) => BlazorFormBuilder.Create()
        .Text("title", f => f.MaxLength(max).CharacterCount().UpdateOnInput())
        .Build();

    private Bunit.IRenderedComponent<BlazorFormView> RenderWith(int max, string value)
    {
        var data = new Dictionary<string, object?> { ["title"] = value };
        return Render<BlazorFormView>(p => p.Add(x => x.Definition, Schema(max)).Add(x => x.Data, data));
    }

    [Fact]
    public void The_visible_counter_stays_hidden_from_assistive_tech()
    {
        var cut = RenderWith(100, "");
        Assert.Equal("true", cut.Find("p.ff-counter").GetAttribute("aria-hidden"));
    }

    [Fact]
    public void Nothing_is_announced_while_the_limit_is_far_away()
    {
        // A number re-announced on every keystroke would bury everything else the field has to say.
        var cut = RenderWith(100, "short");
        Assert.Equal(string.Empty, cut.Find("p.ff-sr-only[aria-live='polite']").TextContent.Trim());
    }

    [Fact]
    public void The_remaining_count_is_announced_as_the_limit_comes_into_view()
    {
        var cut = RenderWith(20, new string('x', 18));
        Assert.Contains("2 characters remaining",
            cut.Find("p.ff-sr-only[aria-live='polite']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Going_over_the_limit_is_announced_as_an_excess()
    {
        var cut = RenderWith(20, new string('x', 23));
        Assert.Contains("3 characters over the limit",
            cut.Find("p.ff-sr-only[aria-live='polite']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void The_announcement_threshold_is_configurable()
    {
        var form = BlazorFormBuilder.Create()
            .Text("title", f => f.MaxLength(200).CharacterCount().Attr("countAnnounceAt", 100))
            .Build();
        var data = new Dictionary<string, object?> { ["title"] = new string('x', 150) };

        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, form).Add(x => x.Data, data));

        Assert.Contains("50 characters remaining",
            cut.Find("p.ff-sr-only[aria-live='polite']").TextContent, StringComparison.Ordinal);
    }
}

public class ObjectGroupDescriptionTests : ComponentTestBase
{
    [Fact]
    public void A_groups_help_text_is_announced_with_the_group()
    {
        // Rendering the explanation without pointing at it leaves it visible but silent.
        var form = BlazorFormBuilder.Create()
            .Object("address", a => a.Text("city"), f => f.Label("Address").Help("Where we should post it."))
            .Build();

        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, form));
        var fieldset = cut.Find("fieldset.ff-group");

        Assert.Equal("ff_address_help", fieldset.GetAttribute("aria-describedby"));
        Assert.Equal("Where we should post it.", cut.Find("#ff_address_help").TextContent);
    }
}

/// <summary>
/// A form-level message names no control, so it cannot be "the first one to go and fix" positionally.
/// It leads instead, which is where a reader looking for what is wrong with the form as a whole looks.
/// </summary>
public class FormLevelMessageOrderingTests : ComponentTestBase
{
    private static BlazorFormDefinition Schema() => BlazorFormBuilder.For<BookingModel>()
        .Field(x => x.Start)
        .Field(x => x.End)
        .MustAll(m => m.End >= m.Start, "End must be on or after start.")
        .Build();

    [Fact]
    public async Task A_form_level_message_leads_the_ordered_list()
    {
        var state = new BlazorFormState(Schema(),
            new BlazorFormModelDataAccessor(new BookingModel { Start = new DateOnly(2026, 2, 1), End = new DateOnly(2026, 1, 1) }));

        state.SetServerError("End", "Something about End.");
        await state.ValidateAsync();
        state.SetServerError("Start", "Something about Start.");

        Assert.Equal("End must be on or after start.", state.OrderedMessages()[0].Message);
    }

    [Fact]
    public async Task The_summary_lists_a_form_level_message_once_not_twice()
    {
        // The view renders form-level messages in a block of their own; with the summary showing, that
        // block would be a second copy of what the summary already lists first.
        var model = new BookingModel { Start = new DateOnly(2026, 2, 1), End = new DateOnly(2026, 1, 1) };
        var state = new BlazorFormState(Schema(), new BlazorFormModelDataAccessor(model));

        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.State, state)
            .Add(x => x.ShowErrorSummary, true));

        await cut.Instance.SubmitAsync();

        var occurrences = cut.Markup.Split("End must be on or after start.").Length - 1;
        Assert.Equal(1, occurrences);
    }
}

public class SchemaDiagnosticTests
{
    [Fact]
    public void Two_fields_asking_for_focus_is_reported()
    {
        // Whichever renders last wins, so the answer would change with the layout rather than with the
        // author's intent.
        var form = BlazorFormBuilder.Create()
            .Text("a", f => f.Autofocus())
            .Text("b", f => f.Autofocus())
            .Build();

        var problem = Assert.Single(form.Validate(), d => d.Message.Contains("Autofocus", StringComparison.Ordinal));

        Assert.Equal(BlazorFormSchemaDiagnosticSeverity.Warning, problem.Severity);
    }

    [Fact]
    public void One_autofocus_is_not_a_problem()
    {
        var form = BlazorFormBuilder.Create().Text("a", f => f.Autofocus()).Text("b").Build();

        Assert.DoesNotContain(form.Validate(), d => d.Message.Contains("Autofocus", StringComparison.Ordinal));
    }
}
