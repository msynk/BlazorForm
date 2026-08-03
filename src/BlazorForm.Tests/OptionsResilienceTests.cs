using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorForm.Tests;

public class CascadingModel
{
    public List<CascadingRow> Rows { get; set; } = [];
}

public class CascadingRow
{
    public string? Country { get; set; }
    public string? City { get; set; }
}

/// <summary>
/// Options that come from a service are the part of a form most likely to misbehave at run time: the
/// dependency lives in another row, or the lookup fails. Neither may take the form down.
/// </summary>
public class CascadingOptionsScopeTests
{
    private static BlazorFormDefinition Schema(BlazorFormOptionsProvider load)
        => BlazorFormBuilder.For<CascadingModel>()
            .Array(x => x.Rows, row => row
                .Field(r => r.Country, f => f.Options(("nl", "Netherlands"), ("be", "Belgium")))
                // "Country" means this row's country, exactly as it does for a condition or a formula.
                .Field(r => r.City, f => f.OptionsFrom(load, "Country")))
            .Build();

    [Fact]
    public async Task A_dependency_named_relative_to_the_row_reloads_that_row()
    {
        var loads = 0;
        var model = new CascadingModel { Rows = [new CascadingRow { Country = "nl" }, new CascadingRow()] };
        var form = Schema(_ =>
        {
            loads++;
            return new ValueTask<IReadOnlyList<BlazorFormSelectOption>>([new BlazorFormSelectOption("a", "A")]);
        });
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));
        var city = form.FindField("Rows")!.ItemTemplate!.Children.Single(c => c.Name == "City");

        await state.EnsureOptionsAsync(city, "Rows[0].City");
        Assert.Equal(1, loads);
        state.SetValue("Rows[0].City", "a");

        // Changing the first row's country must drop that row's options — and clear the now-invalid
        // selection with them.
        state.SetValue("Rows[0].Country", "be");

        Assert.Null(model.Rows[0].City);
        await state.EnsureOptionsAsync(city, "Rows[0].City");
        Assert.Equal(2, loads);
    }

    [Fact]
    public async Task Another_rows_change_leaves_this_rows_options_alone()
    {
        var loads = 0;
        var model = new CascadingModel { Rows = [new CascadingRow { Country = "nl" }, new CascadingRow()] };
        var form = Schema(_ =>
        {
            loads++;
            return new ValueTask<IReadOnlyList<BlazorFormSelectOption>>([new BlazorFormSelectOption("a", "A")]);
        });
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));
        var city = form.FindField("Rows")!.ItemTemplate!.Children.Single(c => c.Name == "City");

        await state.EnsureOptionsAsync(city, "Rows[0].City");
        state.SetValue("Rows[0].City", "a");

        state.SetValue("Rows[1].Country", "be");

        Assert.Equal("a", model.Rows[0].City);
        await state.EnsureOptionsAsync(city, "Rows[0].City");
        Assert.Equal(1, loads);
    }

    [Fact]
    public async Task Naming_a_container_covers_everything_inside_it()
    {
        var loads = 0;
        var form = BlazorFormBuilder.Create()
            .Object("address", a => a.Text("country"))
            .Select("city", f => f.OptionsFrom(_ =>
            {
                loads++;
                return new ValueTask<IReadOnlyList<BlazorFormSelectOption>>([new BlazorFormSelectOption("a", "A")]);
            }, "address"))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        await state.EnsureOptionsAsync(form.FindField("city")!, "city");
        Assert.Equal(1, loads);

        state.SetValue("address.country", "nl");

        await state.EnsureOptionsAsync(form.FindField("city")!, "city");
        Assert.Equal(2, loads);
    }

    [Fact]
    public async Task Options_with_no_declared_dependency_are_loaded_once_and_left_alone()
    {
        var loads = 0;
        var form = BlazorFormBuilder.Create()
            .Text("other")
            .Select("city", f => f.OptionsFrom(_ =>
            {
                loads++;
                return new ValueTask<IReadOnlyList<BlazorFormSelectOption>>([new BlazorFormSelectOption("a", "A")]);
            }))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        await state.EnsureOptionsAsync(form.FindField("city")!, "city");
        state.SetValue("other", "anything");
        await state.EnsureOptionsAsync(form.FindField("city")!, "city");

        Assert.Equal(1, loads);
    }
}

public class FailingOptionsTests : ComponentTestBase
{
    private static BlazorFormDefinition Schema(BlazorFormOptionsProvider load)
        => BlazorFormBuilder.Create().Select("city", f => f.OptionsFrom(load)).Build();

    [Fact]
    public async Task A_provider_that_throws_is_recorded_rather_than_thrown()
    {
        // Renderers call EnsureOptionsAsync from OnParametersSetAsync, so an escaping exception would
        // take the component — and the form around it — down. A lookup over the network fails for
        // entirely ordinary reasons.
        var form = Schema(_ => throw new HttpRequestException("upstream is down"));
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        string? failedPath = null;
        state.OptionsLoadFailed += (path, _) => failedPath = path;

        await state.EnsureOptionsAsync(form.FindField("city")!, "city");

        Assert.Equal("city", failedPath);
        Assert.IsType<HttpRequestException>(state.OptionsError("city"));
        Assert.Empty(state.OptionsFor(form.FindField("city")!, "city"));
    }

    [Fact]
    public async Task A_failed_lookup_retries_and_the_error_clears()
    {
        var attempts = 0;
        var form = Schema(_ =>
        {
            attempts++;
            if (attempts == 1) throw new InvalidOperationException("boom");
            return new ValueTask<IReadOnlyList<BlazorFormSelectOption>>([new BlazorFormSelectOption("a", "A")]);
        });
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        await state.EnsureOptionsAsync(form.FindField("city")!, "city");
        state.InvalidateOptions("city");
        await state.EnsureOptionsAsync(form.FindField("city")!, "city");

        Assert.Null(state.OptionsError("city"));
        Assert.Single(state.OptionsFor(form.FindField("city")!, "city"));
    }

    [Fact]
    public void A_failed_lookup_says_so_instead_of_showing_an_empty_dropdown()
    {
        var form = Schema(_ => throw new InvalidOperationException("boom"));
        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, form));

        // An empty select reads as "there is nothing to choose"; the placeholder says otherwise.
        Assert.Contains("Options could not be loaded", cut.Markup, StringComparison.Ordinal);
    }
}

public class RequiredSelectTests : ComponentTestBase
{
    [Fact]
    public void A_required_selects_placeholder_cannot_be_chosen_again()
    {
        var form = BlazorFormBuilder.Create()
            .Select("topic", f => f.Required().Options(("bug", "Bug"), ("idea", "Idea")))
            .Build();

        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, form));
        var placeholder = cut.Find("select#ff_topic option[value='']");

        Assert.True(placeholder.HasAttribute("disabled"));
    }

    [Fact]
    public void An_optional_select_can_always_go_back_to_nothing()
    {
        var form = BlazorFormBuilder.Create()
            .Select("topic", f => f.Options(("bug", "Bug"), ("idea", "Idea")))
            .Build();

        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, form));

        Assert.False(cut.Find("select#ff_topic option[value='']").HasAttribute("disabled"));
    }
}
