namespace BlazorForm.Tests;

public class ContactRow
{
    public string Kind { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public class ContactBook
{
    public string Kind { get; set; } = "";
    public List<ContactRow> Rows { get; set; } = [];
}

/// <summary>
/// A condition written on a repeater's item template has to mean "this row", not "the field with that
/// name at the root". Before scoping, every row read the root's value and the whole repeater switched
/// together — or, worse, silently read an unrelated field that happened to share a name.
/// </summary>
public class ScopedConditionTests
{
    private static BlazorFormDefinition Schema() => BlazorFormBuilder.For<ContactBook>()
        .Field(x => x.Kind)
        .Array(x => x.Rows, row => row
            .Field(r => r.Kind, f => f.Options(("email", "Email"), ("phone", "Phone")))
            .Field(r => r.Email, f => f.VisibleWhen("Kind", BlazorFormConditionOperator.Equals, "email")
                                       .RequiredWhen("Kind", BlazorFormConditionOperator.Equals, "email"))
            .Field(r => r.Phone, f => f.VisibleWhen("Kind", BlazorFormConditionOperator.Equals, "phone")))
        .Build();

    private static (BlazorFormState State, ContactBook Model) Form(params string[] rowKinds)
    {
        var model = new ContactBook { Kind = "phone" };
        foreach (var kind in rowKinds) model.Rows.Add(new ContactRow { Kind = kind });
        return (new BlazorFormState(Schema(), new BlazorFormModelDataAccessor(model)), model);
    }

    [Fact]
    public void Each_row_evaluates_its_own_value()
    {
        var (state, _) = Form("email", "phone");
        var email = state.Definition.FindByPath("Rows[0].Email")!;

        Assert.True(state.IsVisible(email, "Rows[0].Email"));
        Assert.False(state.IsVisible(email, "Rows[1].Email"));
    }

    [Fact]
    public void The_root_field_of_the_same_name_does_not_win()
    {
        // The model's own Kind is "phone"; row 0 is "email". Reading the root would hide row 0's email.
        var (state, _) = Form("email");
        var email = state.Definition.FindByPath("Rows[0].Email")!;

        Assert.True(state.IsVisible(email, "Rows[0].Email"));
    }

    [Fact]
    public async Task Conditional_requiredness_is_per_row()
    {
        var (state, _) = Form("email", "phone");
        await state.ValidateAsync();

        Assert.Single(state.MessagesFor("Rows[0].Email"));
        Assert.Empty(state.MessagesFor("Rows[1].Email"));
    }

    [Fact]
    public async Task A_hidden_row_field_is_not_validated()
    {
        // Row 1 is a phone row, so its Email is hidden — and a rule on a field the user cannot see must
        // never be what stops the form submitting.
        var (state, _) = Form("phone");
        await state.ValidateAsync();

        Assert.Empty(state.MessagesFor("Rows[0].Email"));
    }

    [Fact]
    public void An_absolute_path_still_resolves_from_inside_a_row()
    {
        var form = BlazorFormBuilder.For<ContactBook>()
            .Field(x => x.Kind)
            .Array(x => x.Rows, row => row
                .Field(r => r.Kind)
                .Field(r => r.Email, f => f.VisibleWhen("Kind", BlazorFormConditionOperator.Equals, "email"))
                // Names the root explicitly: the row has no "Rows" of its own to shadow it.
                .Field(r => r.Phone, f => f.VisibleWhen("Rows[0].Kind", BlazorFormConditionOperator.Equals, "phone")))
            .Build();

        var model = new ContactBook { Rows = { new ContactRow { Kind = "phone" } } };
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        Assert.True(state.IsVisible(form.FindByPath("Rows[0].Phone")!, "Rows[0].Phone"));
    }

    [Fact]
    public void ClearOnHide_clears_only_the_row_that_changed()
    {
        var form = BlazorFormBuilder.For<ContactBook>()
            .Field(x => x.Kind)
            .Array(x => x.Rows, row => row
                .Field(r => r.Kind)
                .Field(r => r.Email, f => f
                    .VisibleWhen("Kind", BlazorFormConditionOperator.Equals, "email")
                    .ClearOnHide()))
            .Build();

        var model = new ContactBook
        {
            Rows =
            {
                new ContactRow { Kind = "email", Email = "a@b.c" },
                new ContactRow { Kind = "email", Email = "d@e.f" }
            }
        };
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        state.SetValue("Rows[0].Kind", "phone");

        Assert.Null(model.Rows[0].Email);
        Assert.Equal("d@e.f", model.Rows[1].Email);
    }
}
