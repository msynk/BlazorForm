namespace BlazorForm.Tests;

public class ValidationTriggerTests
{
    private static BlazorFormState Build(BlazorFormValidationTrigger trigger)
    {
        var form = BlazorFormBuilder.Create().Text("name", f => f.Required()).Build();
        return new BlazorFormState(form, new BlazorFormDictionaryDataAccessor())
        {
            ValidationTrigger = trigger,
            RevalidationTrigger = trigger
        };
    }

    [Fact]
    public void OnChange_validates_on_both_change_and_blur()
    {
        var state = Build(BlazorFormValidationTrigger.OnChange);
        Assert.True(state.ShouldValidate("name", BlazorFormValidationTrigger.OnChange));
        Assert.True(state.ShouldValidate("name", BlazorFormValidationTrigger.OnBlur));
    }

    [Fact]
    public void OnBlur_stays_quiet_while_typing()
    {
        var state = Build(BlazorFormValidationTrigger.OnBlur);
        Assert.False(state.ShouldValidate("name", BlazorFormValidationTrigger.OnChange));
        Assert.True(state.ShouldValidate("name", BlazorFormValidationTrigger.OnBlur));
    }

    [Fact]
    public void OnSubmit_never_validates_from_the_field()
    {
        var state = Build(BlazorFormValidationTrigger.OnSubmit);
        Assert.False(state.ShouldValidate("name", BlazorFormValidationTrigger.OnChange));
        Assert.False(state.ShouldValidate("name", BlazorFormValidationTrigger.OnBlur));
    }

    [Fact]
    public async Task Revalidation_trigger_takes_over_once_submitted()
    {
        var form = BlazorFormBuilder.Create().Text("name", f => f.Required()).Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor())
        {
            ValidationTrigger = BlazorFormValidationTrigger.OnSubmit,
            RevalidationTrigger = BlazorFormValidationTrigger.OnChange
        };

        Assert.False(state.ShouldValidate("name", BlazorFormValidationTrigger.OnChange));

        await state.SubmitAsync();

        Assert.True(state.ShouldValidate("name", BlazorFormValidationTrigger.OnChange));
    }
}

public class SubmissionTests
{
    [Fact]
    public async Task Submit_marks_every_field_touched_so_errors_become_visible()
    {
        var form = BlazorFormBuilder.Create()
            .Text("name", f => f.Required())
            .Object("address", a => a.Text("city", c => c.Required()))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        Assert.False(state.IsTouched("name"));

        var valid = await state.SubmitAsync();

        Assert.False(valid);
        Assert.True(state.IsTouched("name"));
        Assert.True(state.IsTouched("address.city"));
        Assert.Equal(1, state.SubmitCount);
        Assert.True(state.IsSubmitted);
    }

    [Fact]
    public async Task A_second_submit_is_ignored_while_the_first_is_running()
    {
        var form = BlazorFormBuilder.Create().Text("name").Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        var gate = new TaskCompletionSource();
        var calls = 0;

        var first = state.SubmitAsync(async _ =>
        {
            Interlocked.Increment(ref calls);
            await gate.Task;
        });

        // Fires while the first submit is still awaiting its handler.
        var second = await state.SubmitAsync(_ =>
        {
            Interlocked.Increment(ref calls);
            return Task.CompletedTask;
        });

        gate.SetResult();
        await first;

        Assert.False(second);
        Assert.Equal(1, calls);
        Assert.Equal(1, state.SubmitCount);
    }

    [Fact]
    public async Task Valid_submit_invokes_the_valid_handler_only()
    {
        var form = BlazorFormBuilder.Create().Text("name", f => f.Required()).Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        state.SetValue("name", "Ada");

        var validCalls = 0;
        var invalidCalls = 0;

        var result = await state.SubmitAsync(
            _ => { validCalls++; return Task.CompletedTask; },
            _ => { invalidCalls++; return Task.CompletedTask; });

        Assert.True(result);
        Assert.Equal(1, validCalls);
        Assert.Equal(0, invalidCalls);
    }
}

public class ResetTests
{
    [Fact]
    public void Reset_restores_initial_values_and_clears_tracking()
    {
        var model = new RegistrationModel { FirstName = "Ada", Age = 30 };
        var form = BlazorFormSchemaGenerator.Generate<RegistrationModel>();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        state.SetValue("FirstName", "Grace");
        state.SetValue("Age", 41);
        state.SetServerError("FirstName", "taken");
        Assert.True(state.IsFormDirty);

        state.Reset();

        Assert.Equal("Ada", model.FirstName);
        Assert.Equal(30, model.Age);
        Assert.False(state.IsFormDirty);
        Assert.False(state.IsTouched("FirstName"));
        Assert.Empty(state.AllMessages);
        Assert.Equal(0, state.SubmitCount);
    }

    [Fact]
    public void Reset_removes_items_added_after_construction()
    {
        var model = new RegistrationModel();
        var form = BlazorFormSchemaGenerator.Generate<RegistrationModel>();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        state.AddArrayItem(form.FindField("Items")!, "Items");
        Assert.Single(model.Items);

        state.Reset();

        Assert.Empty(model.Items);
    }

    [Fact]
    public void AcceptChanges_rebases_the_reset_point()
    {
        var model = new RegistrationModel { FirstName = "Ada" };
        var form = BlazorFormSchemaGenerator.Generate<RegistrationModel>();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        state.SetValue("FirstName", "Grace");
        state.AcceptChanges();
        Assert.False(state.IsFormDirty);

        state.SetValue("FirstName", "Hedy");
        state.Reset();

        Assert.Equal("Grace", model.FirstName);
    }
}

public class ArrayStateTests
{
    private static (BlazorFormState State, RegistrationModel Model, BlazorFormFieldDefinition Items) Setup()
    {
        var model = new RegistrationModel();
        var form = BlazorFormSchemaGenerator.Generate<RegistrationModel>();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));
        return (state, model, form.FindField("Items")!);
    }

    [Fact]
    public void Insert_places_the_item_at_the_requested_index()
    {
        var (state, model, items) = Setup();
        state.AddArrayItem(items, "Items");
        state.SetValue("Items[0].Product", "second");

        var index = state.InsertArrayItem(items, "Items", 0);
        state.SetValue("Items[0].Product", "first");

        Assert.Equal(0, index);
        Assert.Equal("first", model.Items[0].Product);
        Assert.Equal("second", model.Items[1].Product);
    }

    [Fact]
    public void Removing_an_item_moves_the_following_items_messages_up_with_them()
    {
        var (state, _, items) = Setup();
        state.AddArrayItem(items, "Items");
        state.AddArrayItem(items, "Items");
        state.SetServerError("Items[1].Product", "second item is wrong");

        state.RemoveArrayItem("Items", 0);

        Assert.Empty(state.MessagesFor("Items[1].Product"));
        Assert.Single(state.MessagesFor("Items[0].Product"));
        Assert.Equal("second item is wrong", state.MessagesFor("Items[0].Product")[0].Message);
    }

    [Fact]
    public void Inserting_before_an_item_moves_its_messages_down()
    {
        var (state, _, items) = Setup();
        state.AddArrayItem(items, "Items");
        state.SetServerError("Items[0].Product", "wrong");

        state.InsertArrayItem(items, "Items", 0);

        Assert.Single(state.MessagesFor("Items[1].Product"));
        Assert.Empty(state.MessagesFor("Items[0].Product"));
    }

    [Fact]
    public void Min_and_max_item_counts_gate_add_and_remove()
    {
        var model = new RegistrationModel();
        var form = BlazorFormBuilder.For<RegistrationModel>()
            .Array(x => x.Items, i => i.Field(l => l.Product), f => f.Items(min: 1, max: 2))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));
        var items = form.FindField("Items")!;

        state.AddArrayItem(items, "Items");
        Assert.False(state.CanRemoveArrayItem(items, "Items")); // at the minimum
        Assert.True(state.CanAddArrayItem(items, "Items"));

        state.AddArrayItem(items, "Items");
        Assert.False(state.CanAddArrayItem(items, "Items")); // at the maximum
        Assert.True(state.CanRemoveArrayItem(items, "Items"));
    }
}

public class ConditionalStateTests
{
    [Fact]
    public void ClearOnHide_empties_a_field_that_becomes_invisible()
    {
        var form = BlazorFormBuilder.For<SignupModel>()
            .Field(x => x.IsBusiness)
            .Field(x => x.CompanyName, f => f
                .VisibleWhen(nameof(SignupModel.IsBusiness), BlazorFormConditionOperator.IsTrue)
                .ClearOnHide())
            .Build();

        var model = new SignupModel { IsBusiness = true };
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        state.SetValue("CompanyName", "Acme");
        Assert.Equal("Acme", model.CompanyName);

        state.SetValue("IsBusiness", false);

        Assert.Null(model.CompanyName);
    }

    [Fact]
    public void A_value_survives_a_hidden_field_without_ClearOnHide()
    {
        var form = BlazorFormBuilder.For<SignupModel>()
            .Field(x => x.IsBusiness)
            .Field(x => x.CompanyName, f => f.VisibleWhen(nameof(SignupModel.IsBusiness), BlazorFormConditionOperator.IsTrue))
            .Build();

        var model = new SignupModel { IsBusiness = true, CompanyName = "Acme" };
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        state.SetValue("IsBusiness", false);

        Assert.Equal("Acme", model.CompanyName);
    }

    [Fact]
    public async Task RequiredWhen_only_bites_while_its_condition_holds()
    {
        var form = BlazorFormBuilder.For<SignupModel>()
            .Field(x => x.IsBusiness)
            .Field(x => x.CompanyName, f => f.RequiredWhen(nameof(SignupModel.IsBusiness), BlazorFormConditionOperator.IsTrue))
            .Build();

        var model = new SignupModel { IsBusiness = false };
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        Assert.True(await state.ValidateAsync());

        state.SetValue("IsBusiness", true);

        Assert.False(await state.ValidateAsync());
        Assert.Single(state.MessagesFor("CompanyName"));
    }

    [Fact]
    public void IsRequired_reflects_the_conditional_rule()
    {
        var form = BlazorFormBuilder.For<SignupModel>()
            .Field(x => x.IsBusiness)
            .Field(x => x.CompanyName, f => f.RequiredWhen(nameof(SignupModel.IsBusiness), BlazorFormConditionOperator.IsTrue))
            .Build();

        var model = new SignupModel();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));
        var company = form.FindField("CompanyName")!;

        Assert.False(state.IsRequired(company));
        state.SetValue("IsBusiness", true);
        Assert.True(state.IsRequired(company));
    }

    [Fact]
    public void ReadOnly_is_not_the_same_as_disabled()
    {
        // A read-only control must stay focusable and readable by assistive tech, so it is never
        // rendered as `disabled`.
        var form = BlazorFormBuilder.Create().Text("name", f => f.ReadOnly()).Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        var field = form.FindField("name")!;

        Assert.True(state.IsReadOnly(field));
        Assert.False(state.IsDisabled(field));
    }

    [Fact]
    public void Form_level_readonly_covers_every_field()
    {
        var form = BlazorFormBuilder.Create().Text("name").Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor()) { ReadOnly = true };

        Assert.True(state.IsReadOnly(form.FindField("name")!));
    }
}

public class OptionsProviderTests
{
    [Fact]
    public async Task Loads_options_on_demand_and_caches_them()
    {
        var calls = 0;
        var form = BlazorFormBuilder.Create()
            .Select("country", f => f.OptionsFrom(_ =>
            {
                calls++;
                return new ValueTask<IReadOnlyList<BlazorFormSelectOption>>(
                    new List<BlazorFormSelectOption> { new("fr", "France") });
            }))
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        var field = form.FindField("country")!;

        await state.EnsureOptionsAsync(field, "country");
        await state.EnsureOptionsAsync(field, "country");

        Assert.Equal(1, calls);
        Assert.Equal("France", state.OptionsFor(field, "country")[0].Label);
    }

    [Fact]
    public async Task A_dependency_change_reloads_the_options_and_clears_the_stale_selection()
    {
        var form = BlazorFormBuilder.Create()
            .Select("country")
            .Select("city", f => f.OptionsFrom(ctx =>
            {
                var country = ctx.Value("country") as string;
                IReadOnlyList<BlazorFormSelectOption> options = country == "fr"
                    ? [new("paris", "Paris")]
                    : [new("london", "London")];
                return new ValueTask<IReadOnlyList<BlazorFormSelectOption>>(options);
            }, "country"))
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        var city = form.FindField("city")!;

        state.SetValue("country", "fr");
        await state.EnsureOptionsAsync(city, "city");
        state.SetValue("city", "paris");
        Assert.Equal("Paris", state.OptionsFor(city, "city")[0].Label);

        state.SetValue("country", "uk");

        Assert.Null(state.GetValue("city")); // the stale selection is dropped
        await state.EnsureOptionsAsync(city, "city");
        Assert.Equal("London", state.OptionsFor(city, "city")[0].Label);
    }

    [Fact]
    public void Static_options_are_returned_when_there_is_no_provider()
    {
        var form = BlazorFormBuilder.Create().Select("size", f => f.Options(("s", "Small"), ("l", "Large"))).Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        Assert.Equal(2, state.OptionsFor(form.FindField("size")!, "size").Count);
    }
}

public class WizardTests
{
    private static BlazorFormDefinition ThreeSteps() => BlazorFormBuilder.For<SignupModel>()
        .Field(x => x.Email)
        .Field(x => x.IsBusiness)
        .Field(x => x.CompanyName)
        .Field(x => x.Country)
        .Step("contact", s => s.Title("Contact").Fields("Email"))
        .Step("company", s => s.Title("Company").Fields("CompanyName")
            .VisibleWhen(new BlazorFormFieldCondition(nameof(SignupModel.IsBusiness), BlazorFormConditionOperator.IsTrue)))
        .Step("done", s => s.Title("Done").Fields("Country"))
        .Build();

    [Fact]
    public void Step_numbering_skips_hidden_steps()
    {
        var state = new BlazorFormState(ThreeSteps(), new BlazorFormModelDataAccessor(new SignupModel()));

        Assert.Equal(2, state.VisibleSteps.Count);
        Assert.Equal(1, state.CurrentStepNumber);

        state.GoToStep("done");
        // "done" is the third declared step but the second visible one.
        Assert.Equal(2, state.CurrentStepNumber);
    }

    [Fact]
    public void GoToStep_refuses_a_hidden_step()
    {
        var state = new BlazorFormState(ThreeSteps(), new BlazorFormModelDataAccessor(new SignupModel()));

        state.GoToStep("company");

        Assert.Equal(0, state.CurrentStepIndex);
    }

    [Fact]
    public async Task Next_skips_a_hidden_step()
    {
        var model = new SignupModel { Email = "a@b.com", IsBusiness = false };
        var state = new BlazorFormState(ThreeSteps(), new BlazorFormModelDataAccessor(model));

        Assert.True(await state.NextStepAsync());

        Assert.Equal("done", state.CurrentStep!.Id);
        Assert.True(state.IsLastStep);
    }

    [Fact]
    public async Task A_step_only_reports_the_errors_of_its_own_fields()
    {
        var form = BlazorFormBuilder.For<SignupModel>()
            .Field(x => x.Email, f => f.Required())
            .Field(x => x.Country, f => f.Required())
            .Step("one", s => s.Fields("Email"))
            .Step("two", s => s.Fields("Country"))
            .Build();

        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(new SignupModel { Email = "a@b.com" }));

        Assert.True(await state.ValidateStepAsync());
        Assert.Empty(state.MessagesFor("Country"));
    }
}

public class MessageOrderingTests
{
    [Fact]
    public void Messages_are_ordered_by_their_position_in_the_schema()
    {
        var form = BlazorFormBuilder.Create()
            .Text("first", f => f.Required())
            .Text("second", f => f.Required())
            .Text("third", f => f.Required())
            .Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        state.SetServerError("third", "c");
        state.SetServerError("first", "a");
        state.SetServerError("second", "b");

        Assert.Equal(["a", "b", "c"], state.OrderedMessages().Select(m => m.Message));
    }
}
