namespace BlazorForm.Tests;

public class RequiredSemanticsTests
{
    [Fact]
    public async Task A_required_checkbox_must_be_ticked()
    {
        // HTML's own `required` on a checkbox means "must be checked", and "I accept the terms" is
        // the case every form has. Treating `false` as a present value would make the rule useless.
        var form = BlazorFormBuilder.Create().Checkbox("accept", f => f.Required("Please accept.")).Build();
        var data = new BlazorFormDictionaryDataAccessor();
        var validator = new BlazorFormValidator();

        data.SetValue("accept", false);
        Assert.Contains(await validator.ValidateAsync(form, data), m => m.Message == "Please accept.");

        data.SetValue("accept", true);
        Assert.Empty(await validator.ValidateAsync(form, data));
    }

    [Fact]
    public async Task A_required_non_checkbox_boolean_is_satisfied_by_false()
    {
        // Outside a checkbox, `false` is a perfectly good answer.
        var form = BlazorFormBuilder.Create()
            .Radio("optIn", f => f.Options(("true", "Yes"), ("false", "No")).Required())
            .Build();

        var data = new BlazorFormDictionaryDataAccessor();
        data.SetValue("optIn", false);

        Assert.Empty(await new BlazorFormValidator().ValidateAsync(form, data));
    }
}

public class ConversionFeedbackTests
{
    [Fact]
    public void An_unconvertible_entry_is_reported_instead_of_vanishing()
    {
        var model = new TypedModel { Count = 7 };
        var form = BlazorFormSchemaGenerator.Generate<TypedModel>();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        state.SetValue("Count", "not-a-number");

        // The model keeps its last valid value, and the user is told why their entry did not stick.
        Assert.Equal(7, model.Count);
        var message = Assert.Single(state.MessagesFor("Count"));
        Assert.Contains("not-a-number", message.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_valid_entry_clears_the_earlier_conversion_error()
    {
        var model = new TypedModel();
        var form = BlazorFormSchemaGenerator.Generate<TypedModel>();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        state.SetValue("Count", "nope");
        Assert.NotEmpty(state.MessagesFor("Count"));

        state.SetValue("Count", 12);

        Assert.Empty(state.MessagesFor("Count"));
        Assert.Equal(12, model.Count);
    }

    [Fact]
    public async Task A_conversion_error_survives_revalidation()
    {
        var model = new TypedModel();
        var form = BlazorFormSchemaGenerator.Generate<TypedModel>();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        state.SetValue("Count", "nope");

        // A full validation pass rebuilds the message set; the conversion problem is still real.
        Assert.False(await state.ValidateAsync());
        Assert.NotEmpty(state.MessagesFor("Count"));
    }

    [Theory]
    [InlineData("a.b")]
    [InlineData("a[0]")]
    public void The_dictionary_store_reshapes_rather_than_throwing(string path)
    {
        // "a" holds a string, so it cannot also be the object (or list) the next path segment needs.
        // The old code cast blindly and threw; now the scalar is replaced by the required container.
        var data = new BlazorFormDictionaryDataAccessor();
        data.SetValue("a", "scalar");

        data.SetValue(path, 1);

        Assert.False(data.LastWriteFailed);
        Assert.Equal(1, data.GetValue(path));
    }
}

public class OptionsCancellationTests
{
    [Fact]
    public async Task A_superseded_options_load_is_cancelled()
    {
        var started = new TaskCompletionSource();
        var observed = CancellationToken.None;

        var form = BlazorFormBuilder.Create()
            .Select("country")
            .Select("city", f => f.OptionsFrom(async ctx =>
            {
                observed = ctx.CancellationToken;
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, ctx.CancellationToken);
                return (IReadOnlyList<BlazorFormSelectOption>)Array.Empty<BlazorFormSelectOption>();
            }, "country"))
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        var city = form.FindField("city")!;

        var load = state.EnsureOptionsAsync(city, "city").AsTask();
        await started.Task;

        state.InvalidateOptions("city");
        await load;

        Assert.True(observed.IsCancellationRequested);
    }

    [Fact]
    public async Task Disposing_the_state_cancels_an_in_flight_load()
    {
        var started = new TaskCompletionSource();
        var observed = CancellationToken.None;

        var form = BlazorFormBuilder.Create()
            .Select("city", f => f.OptionsFrom(async ctx =>
            {
                observed = ctx.CancellationToken;
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, ctx.CancellationToken);
                return (IReadOnlyList<BlazorFormSelectOption>)Array.Empty<BlazorFormSelectOption>();
            }))
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        var load = state.EnsureOptionsAsync(form.FindField("city")!, "city").AsTask();
        await started.Task;

        state.Dispose();
        await load;

        Assert.True(observed.IsCancellationRequested);
    }
}

public class ConditionalRuleTests
{
    [Fact]
    public async Task When_scopes_a_group_of_rules_to_a_condition()
    {
        var form = BlazorFormBuilder.For<SignupModel>()
            .Field(x => x.IsBusiness)
            .Field(x => x.CompanyName, f => f
                .When(nameof(SignupModel.IsBusiness), BlazorFormConditionOperator.IsTrue, null, w => w
                    .Required("Business accounts need a company name.")
                    .MinLength(3)))
            .Build();

        var model = new SignupModel { CompanyName = "X" };
        var validator = new BlazorFormValidator();

        // Personal account: neither rule applies.
        Assert.Empty(await validator.ValidateAsync(form, new BlazorFormModelDataAccessor(model)));

        model.IsBusiness = true;
        var messages = await validator.ValidateAsync(form, new BlazorFormModelDataAccessor(model));
        Assert.Contains(messages, m => m.FieldPath == "CompanyName");
    }
}

public class StepPathTests
{
    [Fact]
    public async Task A_step_can_own_a_nested_field()
    {
        var form = BlazorFormBuilder.For<RegistrationModel>()
            .Field(x => x.FirstName, f => f.Required())
            .Field(x => x.Address.City, f => f.Required("City is required."))
            .Step("one", s => s.Field<RegistrationModel>(x => x.FirstName))
            .Step("two", s => s.Field<RegistrationModel>(x => x.Address.City))
            .Build();

        Assert.Equal("Address.City", form.Steps[1].Fields.Single());

        var model = new RegistrationModel { FirstName = "Ada" };
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        // Step one passes, so we advance; step two then reports its own nested field.
        Assert.True(await state.NextStepAsync());
        Assert.False(await state.ValidateStepAsync());
        Assert.Equal("City is required.", state.MessagesFor("Address.City")[0].Message);
    }

    [Fact]
    public void Expression_paths_are_available_outside_the_builder()
    {
        Assert.Equal("Address.City", BlazorFormExpressionPath.Of<RegistrationModel, string>(x => x.Address.City));
        Assert.Equal("Age", BlazorFormExpressionPath.Of<RegistrationModel, object?>(x => x.Age));
    }
}
