using Bunit;

namespace BlazorForm.Tests;

/// <summary>
/// Coming back to fix one answer must not cost the user the rest of the wizard. A step already walked
/// past has been validated, so it can be returned to in either direction.
/// </summary>
public class VisitedStepNavigationTests : ComponentTestBase
{
    private static BlazorFormDefinition Schema() => BlazorFormBuilder.Create()
        .Text("a")
        .Text("b")
        .Text("c")
        .Step("one", s => s.Title("One").Fields("a"))
        .Step("two", s => s.Title("Two").Fields("b"))
        .Step("three", s => s.Title("Three").Fields("c"))
        .Build();

    [Fact]
    public async Task A_step_never_reached_is_not_reachable()
    {
        var state = new BlazorFormState(Schema(), new BlazorFormDictionaryDataAccessor());

        Assert.True(state.IsStepReachable(0));
        Assert.False(state.IsStepReachable(1));

        await state.NextStepAsync();

        Assert.True(state.IsStepReachable(1));
        Assert.False(state.IsStepReachable(2));
    }

    [Fact]
    public async Task Going_back_does_not_forget_where_the_user_had_got_to()
    {
        var state = new BlazorFormState(Schema(), new BlazorFormDictionaryDataAccessor());

        await state.NextStepAsync();
        await state.NextStepAsync();
        state.PreviousStep();
        state.PreviousStep();

        Assert.Equal(0, state.CurrentStepIndex);
        Assert.Equal(2, state.FurthestStepIndex);
        Assert.True(state.IsStepReachable(2));
    }

    [Fact]
    public async Task The_stepper_offers_a_link_forward_to_a_step_already_completed()
    {
        var state = new BlazorFormState(Schema(), new BlazorFormDictionaryDataAccessor());
        await state.NextStepAsync();
        await state.NextStepAsync();
        state.PreviousStep();
        state.PreviousStep();

        var cut = Render<BlazorFormView>(p => p.Add(x => x.State, state));
        var links = cut.FindAll("button.ff-stepper__link");

        // Two links: the steps either side of the one being shown, both already walked past.
        Assert.Equal(2, links.Count);

        links[1].Click();
        Assert.Equal(2, state.CurrentStepIndex);
    }

    [Fact]
    public void A_fresh_wizard_offers_no_links_at_all()
    {
        var state = new BlazorFormState(Schema(), new BlazorFormDictionaryDataAccessor());
        var cut = Render<BlazorFormView>(p => p.Add(x => x.State, state));

        Assert.Empty(cut.FindAll("button.ff-stepper__link"));
    }

    [Fact]
    public async Task AllowStepNavigation_false_still_removes_every_link()
    {
        var state = new BlazorFormState(Schema(), new BlazorFormDictionaryDataAccessor());
        await state.NextStepAsync();

        var cut = Render<BlazorFormView>(p => p
            .Add(x => x.State, state)
            .Add(x => x.AllowStepNavigation, false));

        Assert.Empty(cut.FindAll("button.ff-stepper__link"));
    }

    [Fact]
    public async Task Reset_puts_the_wizard_back_to_the_beginning_in_every_sense()
    {
        var state = new BlazorFormState(Schema(), new BlazorFormDictionaryDataAccessor());
        await state.NextStepAsync();
        await state.NextStepAsync();

        state.Reset();

        Assert.Equal(0, state.CurrentStepIndex);
        Assert.Equal(0, state.FurthestStepIndex);
        Assert.False(state.IsStepReachable(1));
    }

    [Fact]
    public async Task A_step_that_a_condition_hides_is_never_reachable_however_far_the_user_got()
    {
        var form = BlazorFormBuilder.Create()
            .Checkbox("skip")
            .Text("a")
            .Text("b")
            .Step("one", s => s.Fields("skip", "a"))
            .Step("two", s => s.Fields("b").VisibleWhen("skip", BlazorFormConditionOperator.IsFalse))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        await state.NextStepAsync();
        Assert.True(state.IsStepReachable(1));

        state.SetValue("skip", true);

        Assert.False(state.IsStepReachable(1));
    }
}

/// <summary>
/// A yes/no question has two branches, and an unanswered one has to fall into exactly one of them.
/// Reading a missing value as neither hid a field under both.
/// </summary>
public class UnansweredBooleanTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(false)]
    public void An_unanswered_checkbox_counts_as_false(object? value)
    {
        Assert.True(BlazorFormConditionEvaluator.Compare(value, BlazorFormConditionOperator.IsFalse, null));
        Assert.False(BlazorFormConditionEvaluator.Compare(value, BlazorFormConditionOperator.IsTrue, null));
    }

    [Fact]
    public void A_ticked_checkbox_is_only_true()
    {
        Assert.True(BlazorFormConditionEvaluator.Compare(true, BlazorFormConditionOperator.IsTrue, null));
        Assert.False(BlazorFormConditionEvaluator.Compare(true, BlazorFormConditionOperator.IsFalse, null));
    }

    [Fact]
    public void A_value_that_is_not_a_boolean_at_all_is_still_neither()
    {
        Assert.False(BlazorFormConditionEvaluator.Compare("maybe", BlazorFormConditionOperator.IsTrue, null));
        Assert.False(BlazorFormConditionEvaluator.Compare("maybe", BlazorFormConditionOperator.IsFalse, null));
    }

    [Fact]
    public void The_two_branches_of_one_question_are_mutually_exclusive_on_an_untouched_form()
    {
        var form = BlazorFormBuilder.Create()
            .Checkbox("isBusiness")
            .Text("company", f => f.VisibleWhen("isBusiness", BlazorFormConditionOperator.IsTrue))
            .Text("personal", f => f.VisibleWhen("isBusiness", BlazorFormConditionOperator.IsFalse))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        Assert.False(state.IsVisible(form.FindField("company")!));
        Assert.True(state.IsVisible(form.FindField("personal")!));
    }
}

public class RepeaterRowLabellingTests : ComponentTestBase
{
    [Fact]
    public void Each_row_is_a_group_a_screen_reader_can_name()
    {
        // Every row repeats the same field labels, so without a name on the row a screen reader
        // announces "Product, edit" once per line with nothing to tell them apart.
        var model = new RegistrationModel { Items = { new LineItem(), new LineItem() } };
        var form = BlazorFormBuilder.Create()
            .Array("Items", item => item.Text("Product"), f => f.Attr("itemNoun", "line"))
            .Build();

        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, form).Add(x => x.Model, model));
        var rows = cut.FindAll("div.ff-array__item[role=group]");

        Assert.Equal(["line 1", "line 2"], rows.Select(r => r.GetAttribute("aria-label")));
    }

    [Fact]
    public void The_row_name_goes_through_the_message_provider()
    {
        var model = new RegistrationModel { Items = { new LineItem() } };
        var form = BlazorFormBuilder.Create().Array("Items", item => item.Text("Product")).Build();

        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, form).Add(x => x.Model, model));

        Assert.Equal("item 1", cut.Find("div.ff-array__item[role=group]").GetAttribute("aria-label"));
    }
}
