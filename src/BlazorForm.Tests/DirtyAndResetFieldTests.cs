namespace BlazorForm.Tests;

/// <summary>
/// Dirtiness is a comparison against the values the form opened with, not a "has been written to" flag.
/// An undo button, an unsaved-changes prompt and a disabled save button all read it, and every one of
/// them is wrong if typing a character and deleting it again counts as a change.
/// </summary>
public class DirtyIsAComparisonTests
{
    private static BlazorFormState Form(RegistrationModel model)
        => new(BlazorFormSchemaGenerator.Generate<RegistrationModel>(), new BlazorFormModelDataAccessor(model));

    [Fact]
    public void Putting_a_value_back_makes_the_field_clean_again()
    {
        var state = Form(new RegistrationModel { FirstName = "Ada" });

        state.SetValue("FirstName", "Grace");
        Assert.True(state.IsDirty("FirstName"));

        state.SetValue("FirstName", "Ada");

        Assert.False(state.IsDirty("FirstName"));
        Assert.False(state.IsFormDirty);
    }

    [Fact]
    public void Writing_the_same_value_is_not_a_change()
    {
        var state = Form(new RegistrationModel { Age = 30 });

        state.SetValue("Age", 30);

        Assert.False(state.IsFormDirty);
    }

    [Fact]
    public void The_field_stays_touched_even_once_it_is_clean_again()
    {
        // "Has been visited" and "has been changed" are different questions; only the second one is
        // answered by a comparison.
        var state = Form(new RegistrationModel { FirstName = "Ada" });

        state.SetValue("FirstName", "Grace");
        state.SetValue("FirstName", "Ada");

        Assert.True(state.IsTouched("FirstName"));
        Assert.False(state.IsDirty("FirstName"));
    }

    [Fact]
    public void A_row_added_and_removed_again_leaves_the_list_clean()
    {
        var model = new RegistrationModel();
        var state = Form(model);
        var items = state.Definition.FindField("Items")!;

        state.AddArrayItem(items, "Items");
        Assert.True(state.IsFormDirty);

        state.RemoveArrayItem("Items", 0);

        Assert.False(state.IsFormDirty);
    }

    [Fact]
    public void AcceptChanges_rebases_what_counts_as_clean()
    {
        var state = Form(new RegistrationModel { FirstName = "Ada" });

        state.SetValue("FirstName", "Grace");
        state.AcceptChanges();
        state.SetValue("FirstName", "Ada");

        // "Ada" was the original value, but the baseline moved on, so going back to it is a change.
        Assert.True(state.IsDirty("FirstName"));
    }
}

/// <summary>
/// ResetField is the "undo just this answer" a long form needs. Reset() throws away everything, which
/// is far too blunt when the user only wants one field back.
/// </summary>
public class ResetFieldTests
{
    [Fact]
    public void One_field_goes_back_and_the_rest_stay_put()
    {
        var model = new RegistrationModel { FirstName = "Ada", Email = "ada@example.com" };
        var state = new BlazorFormState(BlazorFormSchemaGenerator.Generate<RegistrationModel>(),
            new BlazorFormModelDataAccessor(model));

        state.SetValue("FirstName", "Grace");
        state.SetValue("Email", "grace@example.com");

        state.ResetField("FirstName");

        Assert.Equal("Ada", model.FirstName);
        Assert.Equal("grace@example.com", model.Email);
        Assert.False(state.IsDirty("FirstName"));
        Assert.True(state.IsDirty("Email"));
    }

    [Fact]
    public void Resetting_a_field_clears_its_messages_and_its_touched_state()
    {
        var model = new RegistrationModel();
        var state = new BlazorFormState(BlazorFormSchemaGenerator.Generate<RegistrationModel>(),
            new BlazorFormModelDataAccessor(model));

        state.SetServerError("FirstName", "already taken");
        Assert.NotEmpty(state.MessagesFor("FirstName"));

        state.ResetField("FirstName");

        Assert.Empty(state.MessagesFor("FirstName"));
        Assert.False(state.IsTouched("FirstName"));
    }

    [Fact]
    public void Resetting_an_object_takes_everything_under_it_with_it()
    {
        var model = new RegistrationModel { Address = { City = "Delft", Street = "Oude Delft" } };
        var state = new BlazorFormState(BlazorFormSchemaGenerator.Generate<RegistrationModel>(),
            new BlazorFormModelDataAccessor(model));

        state.SetValue("Address.City", "Leiden");
        state.SetValue("Address.Street", "Rapenburg");

        state.ResetField("Address");

        Assert.Equal("Delft", model.Address.City);
        Assert.Equal("Oude Delft", model.Address.Street);
        Assert.False(state.IsFormDirty);
    }

    [Fact]
    public void A_path_the_form_has_no_baseline_for_is_emptied()
    {
        // A field inside a row added since construction never had a starting value; "put it back" can
        // only mean "empty it".
        var model = new RegistrationModel();
        var state = new BlazorFormState(BlazorFormSchemaGenerator.Generate<RegistrationModel>(),
            new BlazorFormModelDataAccessor(model));

        state.AddArrayItem(state.Definition.FindField("Items")!, "Items");
        state.SetValue("Items[0].Product", "Widget");

        state.ResetField("Items[0].Product");

        Assert.Null(model.Items[0].Product);
    }
}
