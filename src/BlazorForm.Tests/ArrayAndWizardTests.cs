namespace BlazorForm.Tests;

public class CartLine
{
    public string Product { get; set; } = "";
    public int Quantity { get; set; }
    public List<string> Tags { get; set; } = [];
}

public class Cart
{
    public List<CartLine> Lines { get; set; } = [];
}

/// <summary>
/// Repeater behaviour a user notices immediately when it is wrong: a new row that ignores the defaults
/// the schema declared, and errors that stay behind when a row is moved.
/// </summary>
public class ArrayItemTests
{
    private static BlazorFormDefinition Schema() => BlazorFormBuilder.For<Cart>()
        .Array(x => x.Lines, line => line
            .Field(l => l.Product, f => f.Required())
            .Field(l => l.Quantity, f => f.Default(1)))
        .Build();

    private static (BlazorFormState State, Cart Model) Form()
    {
        var model = new Cart();
        return (new BlazorFormState(Schema(), new BlazorFormModelDataAccessor(model)), model);
    }

    [Fact]
    public void A_new_row_is_seeded_from_the_template_defaults()
    {
        var (state, model) = Form();
        var field = state.Definition.FindField("Lines")!;

        state.AddArrayItem(field, "Lines");

        Assert.Equal(1, model.Lines[0].Quantity);
    }

    [Fact]
    public void Rows_the_form_was_handed_keep_their_own_values()
    {
        // Data loaded from storage is answers, not placeholders: a quantity of 0 was chosen by someone
        // and a default must not quietly overwrite it.
        var model = new Cart { Lines = { new CartLine { Product = "Widget", Quantity = 0 } } };
        _ = new BlazorFormState(Schema(), new BlazorFormModelDataAccessor(model));

        Assert.Equal(0, model.Lines[0].Quantity);
    }

    [Fact]
    public void A_default_reaches_a_non_nullable_value_type_on_a_new_row()
    {
        // A freshly created int already reads as 0, so "only fill in what is missing" would never apply
        // a default of 1 here. The row being brand new is what makes overwriting safe.
        var (state, model) = Form();
        state.AddArrayItem(state.Definition.FindField("Lines")!, "Lines");
        state.AddArrayItem(state.Definition.FindField("Lines")!, "Lines");

        Assert.Equal([1, 1], model.Lines.Select(l => l.Quantity));
    }

    [Fact]
    public async Task Moving_a_row_moves_its_errors_with_it()
    {
        var model = new Cart
        {
            Lines = { new CartLine { Product = "" }, new CartLine { Product = "Filled" } }
        };
        var state = new BlazorFormState(Schema(), new BlazorFormModelDataAccessor(model));
        await state.ValidateAsync();

        Assert.Single(state.MessagesFor("Lines[0].Product"));
        Assert.Empty(state.MessagesFor("Lines[1].Product"));

        state.MoveArrayItem("Lines", 0, 1);

        // The empty product is now row 1, and so is its error.
        Assert.Empty(state.MessagesFor("Lines[0].Product"));
        Assert.Single(state.MessagesFor("Lines[1].Product"));
    }

    [Fact]
    public void Moving_a_row_moves_its_touched_state_with_it()
    {
        var model = new Cart { Lines = { new CartLine(), new CartLine(), new CartLine() } };
        var state = new BlazorFormState(Schema(), new BlazorFormModelDataAccessor(model));

        state.SetValue("Lines[2].Product", "Third");
        Assert.True(state.IsTouched("Lines[2].Product"));

        state.MoveArrayItem("Lines", 2, 0);

        Assert.True(state.IsTouched("Lines[0].Product"));
        Assert.False(state.IsTouched("Lines[2].Product"));
    }

    [Fact]
    public void Duplicating_a_row_copies_its_values_into_a_new_independent_row()
    {
        var model = new Cart { Lines = { new CartLine { Product = "Widget", Quantity = 7 } } };
        var state = new BlazorFormState(Schema(), new BlazorFormModelDataAccessor(model));

        var index = state.DuplicateArrayItem(state.Definition.FindField("Lines")!, "Lines", 0);

        Assert.Equal(1, index);
        Assert.Equal(2, model.Lines.Count);
        Assert.Equal("Widget", model.Lines[1].Product);
        Assert.Equal(7, model.Lines[1].Quantity);
        Assert.NotSame(model.Lines[0], model.Lines[1]);
    }

    [Fact]
    public void A_duplicated_row_does_not_share_its_nested_collection()
    {
        var form = BlazorFormBuilder.For<Cart>()
            .Array(x => x.Lines, line => line
                .Field(l => l.Product)
                .Field(l => l.Tags, f => f.As(BlazorFormFieldType.Array)))
            .Build();
        // The typed Array(...) overload builds object rows, so the string list is described by hand.
        form.FindField("Lines")!.ItemTemplate!.Children.Single(c => c.Name == "Tags").ItemTemplate =
            new BlazorFormFieldDefinition("item", BlazorFormFieldType.Text) { ValueType = typeof(string) };

        var model = new Cart { Lines = { new CartLine { Product = "Widget", Tags = { "red", "small" } } } };
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        state.DuplicateArrayItem(form.FindField("Lines")!, "Lines", 0);

        Assert.Equal(["red", "small"], model.Lines[1].Tags);
        Assert.NotSame(model.Lines[0].Tags, model.Lines[1].Tags);

        model.Lines[1].Tags.Add("new");
        Assert.Equal(2, model.Lines[0].Tags.Count);
    }
}

public class WizardModel
{
    public bool IsBusiness { get; set; }
    public string? CompanyName { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>
/// A step the schema hides must be invisible in every sense: not rendered, not standing on, and not
/// the reason submit is refused.
/// </summary>
public class HiddenStepTests
{
    private static BlazorFormDefinition Schema() => BlazorFormBuilder.For<WizardModel>()
        .Field(x => x.Name, f => f.Required())
        .Field(x => x.IsBusiness)
        .Field(x => x.CompanyName, f => f.Required())
        .Step("who", s => s.Title("Who").Fields("Name", "IsBusiness"))
        .Step("business", s => s.Title("Business").Fields("CompanyName")
            .VisibleWhen(nameof(WizardModel.IsBusiness), BlazorFormConditionOperator.IsTrue))
        .Build();

    private static (BlazorFormState State, WizardModel Model) Form()
    {
        var model = new WizardModel { Name = "Ada" };
        return (new BlazorFormState(Schema(), new BlazorFormModelDataAccessor(model)), model);
    }

    [Fact]
    public async Task A_hidden_steps_fields_do_not_block_submission()
    {
        var (state, _) = Form();

        Assert.True(await state.ValidateAsync());
        Assert.Empty(state.MessagesFor("CompanyName"));
    }

    [Fact]
    public async Task The_same_field_blocks_submission_once_its_step_is_shown()
    {
        var (state, _) = Form();
        state.SetValue("IsBusiness", true);

        Assert.False(await state.ValidateAsync());
        Assert.Single(state.MessagesFor("CompanyName"));
    }

    [Fact]
    public async Task Hiding_the_current_step_moves_the_user_off_it()
    {
        var (state, _) = Form();
        state.SetValue("IsBusiness", true);
        await state.NextStepAsync();
        Assert.Equal("business", state.CurrentStep!.Id);

        // Going back and changing the answer strands the wizard on a step that no longer applies.
        state.SetValue("IsBusiness", false);

        Assert.Equal("who", state.CurrentStep!.Id);
    }
}
