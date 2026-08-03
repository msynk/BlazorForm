using Bunit;

namespace BlazorForm.Tests;

/// <summary>
/// A field decides whether to redraw by comparing its message list's identity. Anything that adds a
/// message therefore has to replace the list, not append to it — or the message is recorded in the
/// state and never reaches the page.
/// </summary>
public class MessageIdentityTests : ComponentTestBase
{
    private (BlazorFormState State, Bunit.IRenderedComponent<BlazorFormView> Cut) Form()
    {
        var form = BlazorFormBuilder.Create().Text("name", f => f.Required()).Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        return (state, Render<BlazorFormView>(p => p.Add(x => x.State, state)));
    }

    [Fact]
    public void A_second_server_error_on_a_field_that_already_reports_one_is_rendered()
    {
        var (state, cut) = Form();

        state.SetServerError("name", "First problem.");
        cut.Render();
        Assert.Single(cut.FindAll("#ff_name_error li"));

        state.SetServerError("name", "Second problem.");
        cut.Render();

        Assert.Equal(2, cut.FindAll("#ff_name_error li").Count);
        Assert.Contains("Second problem.", cut.Find("#ff_name_error").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void The_state_and_the_page_agree_about_how_many_messages_there_are()
    {
        var (state, cut) = Form();
        state.SetServerError("name", "One.");
        state.SetServerError("name", "Two.");
        state.SetServerError("name", "Three.");
        cut.Render();

        Assert.Equal(state.MessagesFor("name").Count, cut.FindAll("#ff_name_error li").Count);
    }

    [Fact]
    public void A_conversion_failure_still_shows_beside_a_rule_that_also_failed()
    {
        var form = BlazorFormBuilder.Create()
            .Field("age", BlazorFormFieldType.Integer, f => f.Required())
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(new Holder()));
        var cut = Render<BlazorFormView>(p => p.Add(x => x.State, state));

        state.SetValue("age", "abc");
        cut.Render();

        Assert.Single(cut.FindAll("#ff_age_error li"));
        Assert.Contains("abc", cut.Find("#ff_age_error").TextContent, StringComparison.Ordinal);
    }

    private sealed class Holder
    {
        public int Age { get; set; }
    }

    [Fact]
    public void SingleErrorPerField_still_keeps_only_the_first()
    {
        var form = BlazorFormBuilder.Create().Text("name").Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor())
        {
            SingleErrorPerField = true
        };

        state.SetServerError("name", "First.");
        state.SetServerError("name", "Second.");

        Assert.Single(state.MessagesFor("name"));
        Assert.Equal("First.", state.MessagesFor("name")[0].Message);
    }
}

/// <summary>
/// A form generated from <c>Article : Document</c> should read like the class does: the base's
/// questions, then the derived one's.
/// </summary>
public class GeneratedFieldOrderTests
{
    // Deliberately declared derived-first. Metadata tokens run in file order, so this is the layout
    // that used to reverse the form.
    private class Article : Document
    {
        public string? Body { get; set; }
        public string? Tags { get; set; }
    }

    private class Document
    {
        public string? Title { get; set; }
        public string? Author { get; set; }
    }

    private sealed class Draft : Article
    {
        public bool Published { get; set; }
    }

    [Fact]
    public void Inherited_fields_come_before_the_type_s_own()
    {
        var names = BlazorFormSchemaGenerator.Generate<Article>().Fields.Select(f => f.Name).ToList();

        Assert.Equal(["Title", "Author", "Body", "Tags"], names);
    }

    [Fact]
    public void Three_levels_read_from_the_root_down()
    {
        var names = BlazorFormSchemaGenerator.Generate<Draft>().Fields.Select(f => f.Name).ToList();

        Assert.Equal(["Title", "Author", "Body", "Tags", "Published"], names);
    }

    [Fact]
    public void Display_Order_still_wins_over_the_declaration_order()
    {
        var names = BlazorFormSchemaGenerator.Generate<Ordered>().Fields.Select(f => f.Name).ToList();

        Assert.Equal(["Last", "First"], names);
    }

    private sealed class Ordered : OrderedBase
    {
        [System.ComponentModel.DataAnnotations.Display(Order = -1)]
        public string? Last { get; set; }
    }

    private class OrderedBase
    {
        public string? First { get; set; }
    }
}

/// <summary>Configuring the state the view builds for you, without having to build it yourself.</summary>
public class ConfigureStateTests : ComponentTestBase
{
    private sealed class Contact
    {
        public string? Name { get; set; }
    }

    [Fact]
    public void The_callback_sees_the_state_the_view_built()
    {
        var form = BlazorFormBuilder.Create().Text("Name", f => f.Required()).Build();
        BlazorFormState? seen = null;

        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, form)
            .Add(x => x.Model, new Contact())
            .Add(x => x.ConfigureState, s =>
            {
                seen = s;
                s.ValidationTrigger = BlazorFormValidationTrigger.OnBlur;
                s.SingleErrorPerField = true;
            }));

        Assert.NotNull(seen);
        Assert.Same(seen, cut.Instance.Form);
        Assert.Equal(BlazorFormValidationTrigger.OnBlur, cut.Instance.Form.ValidationTrigger);
        Assert.True(cut.Instance.Form.SingleErrorPerField);
    }

    [Fact]
    public void It_runs_once_per_state_rather_than_once_per_render()
    {
        var form = BlazorFormBuilder.Create().Text("Name").Build();
        var model = new Contact();
        var calls = 0;

        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, form)
            .Add(x => x.Model, model)
            .Add(x => x.ConfigureState, _ => calls++));

        cut.Render();
        cut.Render();

        Assert.Equal(1, calls);
    }

    [Fact]
    public void A_new_schema_gets_a_new_state_and_a_fresh_configuration()
    {
        var first = BlazorFormBuilder.Create().Text("Name").Build();
        var second = BlazorFormBuilder.Create().Text("Name").Build();
        var model = new Contact();
        var calls = 0;

        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, first)
            .Add(x => x.Model, model)
            .Add(x => x.ConfigureState, _ => calls++));

        cut.Render(p => p
            .Add(x => x.Definition, second)
            .Add(x => x.Model, model)
            .Add(x => x.ConfigureState, _ => calls++));

        Assert.Equal(2, calls);
    }

    [Fact]
    public void A_caller_supplied_state_is_left_alone_because_it_is_already_theirs_to_configure()
    {
        var form = BlazorFormBuilder.Create().Text("Name").Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        var calls = 0;

        Render<BlazorFormView>(p => p
            .Add(x => x.State, state)
            .Add(x => x.ConfigureState, _ => calls++));

        Assert.Equal(0, calls);
    }
}
