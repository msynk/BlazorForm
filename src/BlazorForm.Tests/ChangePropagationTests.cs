using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorForm.Tests;

/// <summary>
/// Every way a value can change has to look the same to the rest of the engine. A value the user typed
/// already ran the sweeps — clear the branches this answer closed, refresh what reads it, drop the
/// options that cascade off it, tell the listeners. A value the *form* changed on the user's behalf
/// was skipping them, which is how a computed field came to be invisible to conditions in an earlier
/// round; clearing a hidden field and undoing a single answer had the same hole.
/// </summary>
public class ChangePropagationTests
{
    private static double Num(object? value) => value is null ? 0 : Convert.ToDouble(value);

    /// <summary>An options provider that reports whichever country is current, so a stale cache shows.</summary>
    private static BlazorFormOptionsProvider CountryEcho => ctx =>
        new ValueTask<IReadOnlyList<BlazorFormSelectOption>>(
            (IReadOnlyList<BlazorFormSelectOption>)[
                new BlazorFormSelectOption(ctx.Value("Country") as string ?? "none", "City")]);

    [Fact]
    public void Clearing_a_hidden_field_is_announced_like_any_other_change()
    {
        var form = BlazorFormBuilder.Create()
            .Checkbox("Shipping")
            .Text("City", f => f.VisibleWhen("Shipping", BlazorFormConditionOperator.IsTrue).ClearOnHide())
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        var changed = new List<string>();

        state.SetValue("Shipping", true);
        state.SetValue("City", "Lisbon");
        state.FieldChanged += changed.Add;

        state.SetValue("Shipping", false);

        // An autosave, a live preview or a page-level dirty prompt watching FieldChanged must hear
        // that City is now empty — otherwise it saves a city the model no longer holds.
        Assert.Contains("City", changed);
        Assert.Null(state.GetValue("City"));
    }

    [Fact]
    public void Clearing_a_hidden_field_refreshes_a_total_that_reads_it()
    {
        var form = BlazorFormBuilder.Create()
            .Checkbox("Extras")
            .Number("Extra", f => f.VisibleWhen("Extras", BlazorFormConditionOperator.IsTrue).ClearOnHide())
            .Number("Base")
            .Number("Total", f => f.ReadOnly()
                .Computed(ctx => Num(ctx.Sibling("Base")) + Num(ctx.Sibling("Extra")), "Base", "Extra"))
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        state.SetValue("Base", 10d);
        state.SetValue("Extras", true);
        state.SetValue("Extra", 5d);
        Assert.Equal(15d, Num(state.GetValue("Total")));

        state.SetValue("Extras", false);

        // The extra is gone from the model; a total still claiming 15 is simply wrong.
        Assert.Equal(10d, Num(state.GetValue("Total")));
    }

    [Fact]
    public async Task Clearing_a_hidden_field_drops_the_options_that_cascade_off_it()
    {
        var form = BlazorFormBuilder.Create()
            .Checkbox("Custom")
            .Text("Country", f => f.VisibleWhen("Custom", BlazorFormConditionOperator.IsTrue).ClearOnHide())
            .Select("City", f => f.OptionsFrom(CountryEcho, "Country"))
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        var city = form.FindByPath("City")!;

        state.SetValue("Custom", true);
        state.SetValue("Country", "France");
        await state.EnsureOptionsAsync(city, "City");
        Assert.Equal("France", state.OptionsFor(city, "City")[0].Value);

        state.SetValue("Custom", false);

        // The country the list was built for no longer exists, so the cached list must not survive it.
        await state.EnsureOptionsAsync(city, "City");
        Assert.Equal("none", state.OptionsFor(city, "City")[0].Value);
    }

    [Fact]
    public void ResetField_runs_the_same_sweeps_a_typed_value_does()
    {
        var form = BlazorFormBuilder.Create()
            .Number("Quantity")
            .Number("Total", f => f.ReadOnly().Computed(ctx => Num(ctx.Sibling("Quantity")) * 2, "Quantity"))
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor(
            new Dictionary<string, object?> { ["Quantity"] = 3d }));
        var changed = new List<string>();
        state.FieldChanged += changed.Add;

        state.SetValue("Quantity", 10d);
        Assert.Equal(20d, Num(state.GetValue("Total")));

        changed.Clear();
        state.ResetField("Quantity");

        Assert.Equal(3d, Num(state.GetValue("Quantity")));
        Assert.Equal(6d, Num(state.GetValue("Total")));
        Assert.Contains("Quantity", changed);
    }

    [Fact]
    public async Task ResetField_drops_the_options_that_cascade_off_it()
    {
        var form = BlazorFormBuilder.Create()
            .Text("Country")
            .Select("City", f => f.OptionsFrom(CountryEcho, "Country"))
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor(
            new Dictionary<string, object?> { ["Country"] = "France" }));
        var city = form.FindByPath("City")!;

        state.SetValue("Country", "Spain");
        await state.EnsureOptionsAsync(city, "City");
        Assert.Equal("Spain", state.OptionsFor(city, "City")[0].Value);

        state.ResetField("Country");

        await state.EnsureOptionsAsync(city, "City");
        Assert.Equal("France", state.OptionsFor(city, "City")[0].Value);
    }

    [Fact]
    public void A_tag_added_and_taken_back_leaves_the_form_clean()
    {
        // Same shape as the multi-select: every edit writes a new list, so a baseline held by
        // reference reported the field dirty from the first tag and never clean again.
        var form = BlazorFormBuilder.Create().Tags("Skills").Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor(
            new Dictionary<string, object?> { ["Skills"] = new List<string> { "csharp" } }));

        state.SetValue("Skills", new List<string> { "csharp", "blazor" });
        Assert.True(state.IsDirty("Skills"));

        state.SetValue("Skills", new List<string> { "csharp" });
        Assert.False(state.IsDirty("Skills"));
        Assert.False(state.IsFormDirty);
    }

    [Fact]
    public void Resetting_restores_a_collection_the_user_emptied()
    {
        var form = BlazorFormBuilder.Create().Tags("Skills").Build();
        var model = new Dictionary<string, object?> { ["Skills"] = new List<string> { "csharp" } };
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor(model));

        // Emptied to nothing at all, not to an empty list — which is what ClearOnHide and a Clear
        // button both do, and what used to leave Reset with no live list to refill.
        state.SetValue("Skills", null);
        state.Reset();

        Assert.Equal(["csharp"], ((System.Collections.IEnumerable)state.GetValue("Skills")!)
            .Cast<object?>().Select(v => v?.ToString()));
    }

    [Fact]
    public void Clearing_a_hidden_field_settles_rather_than_recursing()
    {
        // Hiding one field clears it, which hides the next, which clears it. The sweep has to reach
        // the end of the chain and stop, not run out of stack.
        var form = BlazorFormBuilder.Create()
            .Checkbox("A")
            .Text("B", f => f.VisibleWhen("A", BlazorFormConditionOperator.IsTrue).ClearOnHide())
            .Text("C", f => f.VisibleWhen("B", BlazorFormConditionOperator.IsNotEmpty).ClearOnHide())
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        state.SetValue("A", true);
        state.SetValue("B", "x");
        state.SetValue("C", "y");

        state.SetValue("A", false);

        Assert.Null(state.GetValue("B"));
        Assert.Null(state.GetValue("C"));
    }
}

/// <summary>
/// A multi-select's value is a set the user assembles by clicking, but the order it is stored in is
/// the schema's, not the click sequence's. Otherwise choosing A then B and choosing B then A produce
/// different data for the same answer — and a form put back exactly as it was reports unsaved changes.
/// </summary>
public class MultiSelectOrderTests : BunitContext
{
    public MultiSelectOrderTests() => Services.AddBlazorForm();

    private static BlazorFormDefinition Form() => BlazorFormBuilder.Create()
        .MultiSelect("Days", f => f.Options(("mon", "Monday"), ("tue", "Tuesday"), ("wed", "Wednesday")))
        .Build();

    private static List<string> Stored(BlazorFormState state)
        => ((System.Collections.IEnumerable)state.GetValue("Days")!).Cast<object?>()
            .Select(v => v?.ToString() ?? "").ToList();

    [Fact]
    public void The_stored_order_follows_the_options_not_the_clicks()
    {
        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, Form()));
        var boxes = cut.FindAll(".ff-multiselect input[type=checkbox]");

        // Chosen back to front.
        boxes[2].Change(true);
        cut.FindAll(".ff-multiselect input[type=checkbox]")[0].Change(true);

        Assert.Equal(["mon", "wed"], Stored(cut.Instance.Form));
    }

    [Fact]
    public void A_flags_enum_multiselect_still_compares_as_one_value()
    {
        // It renders as a set of boxes but stores one combined value; snapshotting it as a collection
        // would leave it dirty for ever.
        var form = BlazorFormSchemaGenerator.Generate<TypedModel>();
        var model = new TypedModel { Availability = Days.Monday };
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        state.SetValue("Availability", Days.Monday | Days.Tuesday);
        Assert.True(state.IsDirty("Availability"));

        state.SetValue("Availability", Days.Monday);
        Assert.False(state.IsDirty("Availability"));
    }

    [Fact]
    public void An_unchanged_selection_leaves_the_form_clean()
    {
        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.Definition, Form())
            .Add(x => x.Data, new Dictionary<string, object?> { ["Days"] = new List<string> { "mon", "wed" } }));
        var state = cut.Instance.Form;

        // Untick Monday, tick it again. The same two days are chosen, so nothing has changed.
        cut.FindAll(".ff-multiselect input[type=checkbox]")[0].Change(false);
        Assert.True(state.IsDirty("Days"));

        cut.FindAll(".ff-multiselect input[type=checkbox]")[0].Change(true);

        Assert.Equal(["mon", "wed"], Stored(state));
        Assert.False(state.IsDirty("Days"));
        Assert.False(state.IsFormDirty);
    }
}
