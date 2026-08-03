namespace BlazorForm.Tests;

/// <summary>
/// The state API a page reaches for once a form outlives a single render: rebasing onto a saved
/// record, triggering one field, asking for everything known about a field at once.
/// </summary>
public class ResetWithValuesTests
{
    private static BlazorFormState Form(RegistrationModel model)
        => new(BlazorFormSchemaGenerator.Generate<RegistrationModel>(), new BlazorFormModelDataAccessor(model));

    [Fact]
    public void The_new_values_are_written_and_become_the_baseline()
    {
        var model = new RegistrationModel { FirstName = "Ada" };
        var state = Form(model);

        state.Reset(new Dictionary<string, object?> { ["FirstName"] = "Grace", ["Age"] = 45 });

        Assert.Equal("Grace", model.FirstName);
        Assert.Equal(45, model.Age);
        // Rebased, not merely written: the form opened on "Grace" as far as anything reading dirtiness
        // is concerned.
        Assert.False(state.IsFormDirty);
    }

    [Fact]
    public void Reset_afterwards_goes_back_to_the_rebased_values_not_the_original_ones()
    {
        var model = new RegistrationModel { FirstName = "Ada" };
        var state = Form(model);

        state.Reset(new Dictionary<string, object?> { ["FirstName"] = "Grace" });
        state.SetValue("FirstName", "Katherine");
        state.Reset();

        Assert.Equal("Grace", model.FirstName);
    }

    [Fact]
    public void Everything_the_previous_session_accumulated_is_cleared()
    {
        var model = new RegistrationModel();
        var state = Form(model);

        state.SetServerError("Email", "already registered");
        state.MarkTouched("Email");
        state.RegisterSubmitAttempt();

        state.Reset(new Dictionary<string, object?> { ["Email"] = "ada@example.com" });

        Assert.Empty(state.MessagesFor("Email"));
        Assert.False(state.IsTouched("Email"));
        Assert.Equal(0, state.SubmitCount);
        Assert.False(state.HasValidated);
    }

    [Fact]
    public void A_field_the_caller_did_not_name_keeps_its_value_and_is_baselined_at_it()
    {
        var model = new RegistrationModel { FirstName = "Ada", Email = "ada@example.com" };
        var state = Form(model);

        state.Reset(new Dictionary<string, object?> { ["FirstName"] = "Grace" });

        Assert.Equal("ada@example.com", model.Email);
        Assert.False(state.IsDirty("Email"));
    }

    [Fact]
    public void Computed_fields_are_seeded_against_the_new_data_rather_than_left_dirty()
    {
        var model = new OrderModel();
        var form = BlazorFormBuilder.For<OrderModel>()
            .Field(x => x.FirstName)
            .Field(x => x.LastName)
            .Computed(x => x.FullName, m => $"{m.FirstName} {m.LastName}".Trim(),
                dependsOn: [nameof(OrderModel.FirstName), nameof(OrderModel.LastName)])
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        state.Reset(new Dictionary<string, object?> { ["FirstName"] = "Grace", ["LastName"] = "Hopper" });

        Assert.Equal("Grace Hopper", model.FullName);
        Assert.False(state.IsFormDirty);
    }

    [Fact]
    public void A_null_collection_is_rejected_rather_than_silently_doing_nothing()
        => Assert.Throws<ArgumentNullException>(() => Form(new RegistrationModel()).Reset(null!));
}

public class ValidateFieldByPathTests
{
    private static BlazorFormState Form()
        => new(BlazorFormSchemaGenerator.Generate<RegistrationModel>(),
            new BlazorFormModelDataAccessor(new RegistrationModel()));

    [Fact]
    public async Task A_path_is_enough_to_validate_one_field()
    {
        var state = Form();

        Assert.True(await state.ValidateFieldAsync("FirstName"));

        Assert.NotEmpty(state.MessagesFor("FirstName"));
        // Only that field: nothing else was judged.
        Assert.Empty(state.MessagesFor("Email"));
    }

    [Fact]
    public async Task A_nested_path_resolves_through_the_schema()
    {
        var state = Form();

        Assert.True(await state.ValidateFieldAsync("Address.City"));

        Assert.NotEmpty(state.MessagesFor("Address.City"));
    }

    [Fact]
    public async Task An_unknown_path_reports_false_instead_of_throwing()
        => Assert.False(await Form().ValidateFieldAsync("NotAField"));

    [Fact]
    public async Task An_empty_path_is_a_programming_error()
        => await Assert.ThrowsAsync<ArgumentException>(async () => await Form().ValidateFieldAsync(""));
}

public class FieldStateTests
{
    [Fact]
    public async Task Everything_known_about_a_field_arrives_in_one_read()
    {
        var model = new RegistrationModel { FirstName = "Ada" };
        var state = new BlazorFormState(BlazorFormSchemaGenerator.Generate<RegistrationModel>(),
            new BlazorFormModelDataAccessor(model));

        state.SetValue("FirstName", "");
        await state.ValidateFieldAsync("FirstName");

        var field = state.GetFieldState("FirstName");

        Assert.True(field.IsTouched);
        Assert.True(field.IsDirty);
        Assert.True(field.IsInvalid);
        Assert.NotNull(field.Error);
    }

    [Fact]
    public void An_untouched_valid_field_reports_nothing_at_all()
    {
        var state = new BlazorFormState(BlazorFormSchemaGenerator.Generate<RegistrationModel>(),
            new BlazorFormModelDataAccessor(new RegistrationModel()));

        var field = state.GetFieldState("FirstName");

        Assert.False(field.IsTouched);
        Assert.False(field.IsDirty);
        Assert.False(field.IsInvalid);
        Assert.Null(field.Error);
        Assert.Empty(field.Messages);
    }

    [Fact]
    public void A_warning_does_not_make_a_field_invalid()
    {
        var form = BlazorFormBuilder.Create().Text("a").Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        state.SetServerErrors([new BlazorFormValidationMessage("a", "Looks unusual.", BlazorFormValidationSeverity.Warning)]);

        var field = state.GetFieldState("a");

        Assert.False(field.IsInvalid);
        Assert.Null(field.Error);
        Assert.Single(field.Messages);
    }
}

/// <summary>
/// A field breaking four rules at once reads as four problems rather than one field to go and fix.
/// React Hook Form defaults to showing the first; this library offers it as a choice.
/// </summary>
public class SingleErrorPerFieldTests
{
    private static BlazorFormDefinition Schema() => BlazorFormBuilder.Create()
        .Text("password", f => f
            .Required()
            .MinLength(8)
            .Pattern(@"^\d+$", "Digits only."))
        .Build();

    [Fact]
    public async Task Off_by_default_every_broken_rule_is_reported()
    {
        var state = new BlazorFormState(Schema(), new BlazorFormDictionaryDataAccessor());
        state.SetValue("password", "ab");

        await state.ValidateAsync();

        Assert.True(state.MessagesFor("password").Count > 1);
    }

    [Fact]
    public async Task On_only_the_first_error_is_kept()
    {
        var state = new BlazorFormState(Schema(), new BlazorFormDictionaryDataAccessor())
        {
            SingleErrorPerField = true
        };
        state.SetValue("password", "ab");

        await state.ValidateAsync();

        Assert.Single(state.MessagesFor("password"));
    }

    [Fact]
    public async Task The_verdict_is_unchanged_only_the_reporting_is()
    {
        var state = new BlazorFormState(Schema(), new BlazorFormDictionaryDataAccessor())
        {
            SingleErrorPerField = true
        };
        state.SetValue("password", "ab");

        Assert.False(await state.ValidateAsync());
        Assert.False(state.IsValid);
    }

    [Fact]
    public async Task A_warning_still_sits_alongside_the_error()
    {
        var form = BlazorFormBuilder.Create()
            .Text("a", f => f
                .Required()
                .MinLength(5)
                .Validate(new BlazorFormDelegateRule(_ =>
                    BlazorFormRuleResult.Fail("Looks unusual.", BlazorFormValidationSeverity.Warning))))
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor()) { SingleErrorPerField = true };
        state.SetValue("a", "ab");

        await state.ValidateAsync();

        var messages = state.MessagesFor("a");
        Assert.Single(messages, m => m.Severity == BlazorFormValidationSeverity.Error);
        Assert.Single(messages, m => m.Severity == BlazorFormValidationSeverity.Warning);
    }
}

/// <summary>
/// Swap and clear: the two repeater operations that were missing, and that doing by hand with the
/// existing ones gets subtly wrong.
/// </summary>
public class RepeaterSwapAndClearTests
{
    private static (BlazorFormState State, RegistrationModel Model) FormWithRows(int rows)
    {
        var model = new RegistrationModel();
        var form = BlazorFormSchemaGenerator.Generate<RegistrationModel>();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));
        var items = form.FindField("Items")!;

        for (var i = 0; i < rows; i++)
        {
            state.AddArrayItem(items, "Items");
            state.SetValue($"Items[{i}].Product", $"P{i}");
        }
        return (state, model);
    }

    [Fact]
    public void Swapping_exchanges_two_rows_and_leaves_the_rest_where_they_were()
    {
        var (state, model) = FormWithRows(4);

        state.SwapArrayItems("Items", 0, 3);

        Assert.Equal(["P3", "P1", "P2", "P0"], model.Items.Select(i => i.Product));
    }

    [Fact]
    public void A_swapped_row_takes_its_errors_with_it()
    {
        var (state, _) = FormWithRows(3);
        state.SetServerError("Items[0].Product", "duplicate");

        state.SwapArrayItems("Items", 0, 2);

        Assert.Empty(state.MessagesFor("Items[0].Product"));
        Assert.Single(state.MessagesFor("Items[2].Product"));
    }

    [Fact]
    public void Swapping_differs_from_moving_which_shuffles_everything_in_between()
    {
        var (swapState, swapModel) = FormWithRows(4);
        var (moveState, moveModel) = FormWithRows(4);

        swapState.SwapArrayItems("Items", 0, 3);
        moveState.MoveArrayItem("Items", 0, 3);

        Assert.Equal(["P3", "P1", "P2", "P0"], swapModel.Items.Select(i => i.Product));
        Assert.Equal(["P1", "P2", "P3", "P0"], moveModel.Items.Select(i => i.Product));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 9)]
    [InlineData(1, 1)]
    public void An_impossible_swap_is_ignored(int first, int second)
    {
        var (state, model) = FormWithRows(3);

        state.SwapArrayItems("Items", first, second);

        Assert.Equal(["P0", "P1", "P2"], model.Items.Select(i => i.Product));
    }

    [Fact]
    public void Clearing_empties_the_list_and_everything_keyed_to_its_rows()
    {
        var (state, model) = FormWithRows(3);
        state.SetServerError("Items[1].Product", "duplicate");
        state.MarkTouched("Items[1].Product");

        state.ClearArrayItems("Items");

        Assert.Empty(model.Items);
        Assert.Empty(state.MessagesFor("Items[1].Product"));
        Assert.False(state.IsTouched("Items[1].Product"));
    }

    [Fact]
    public void Clearing_a_list_the_form_opened_with_is_a_change()
    {
        var model = new RegistrationModel { Items = { new LineItem { Product = "P0" } } };
        var state = new BlazorFormState(BlazorFormSchemaGenerator.Generate<RegistrationModel>(),
            new BlazorFormModelDataAccessor(model));

        state.ClearArrayItems("Items");

        Assert.True(state.IsDirty("Items"));
        Assert.True(state.IsFormDirty);
    }

    [Fact]
    public void Clearing_rows_added_since_construction_puts_the_list_back_where_it_started()
    {
        // Dirtiness is a comparison, so emptying a list that opened empty is not a change — the same
        // reasoning that makes a row added and removed again leave the form clean.
        var (state, _) = FormWithRows(3);

        state.ClearArrayItems("Items");

        Assert.False(state.IsDirty("Items"));
    }

    [Fact]
    public void Clearing_an_already_empty_list_does_nothing()
    {
        var model = new RegistrationModel();
        var state = new BlazorFormState(BlazorFormSchemaGenerator.Generate<RegistrationModel>(),
            new BlazorFormModelDataAccessor(model));

        state.ClearArrayItems("Items");

        Assert.False(state.IsFormDirty);
    }

    [Fact]
    public void Clearing_refreshes_a_total_computed_over_the_list()
    {
        var model = new OrderModel();
        var form = BlazorFormBuilder.For<OrderModel>()
            .Array(x => x.Lines, line => line.Field(l => l.UnitPrice))
            .Computed(x => x.Total, m => m.Lines.Sum(l => l.UnitPrice), dependsOn: [nameof(OrderModel.Lines)])
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        state.AddArrayItem(form.FindField("Lines")!, "Lines");
        state.SetValue("Lines[0].UnitPrice", 12m);
        Assert.Equal(12m, model.Total);

        state.ClearArrayItems("Lines");

        Assert.Equal(0m, model.Total);
    }
}

/// <summary>
/// A derived value is a change to the form like any other. Everything a typed-in value triggers has to
/// happen for a computed one too, or the schema's own calculations are the one kind of change the rest
/// of the engine cannot see.
/// </summary>
public class ComputedValueSideEffectTests
{
    [Fact]
    public void A_field_hidden_by_a_computed_value_is_cleared()
    {
        var form = BlazorFormBuilder.Create()
            .Number("price")
            .Number("quantity")
            .Number("total", f => f.Computed(
                ctx => Convert.ToDouble(ctx.Sibling("price") ?? 0d) * Convert.ToDouble(ctx.Sibling("quantity") ?? 0d),
                "price", "quantity"))
            .Text("approver", f => f
                .VisibleWhen("total", BlazorFormConditionOperator.GreaterThan, 100d)
                .ClearOnHide())
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        state.SetValue("price", 60d);
        state.SetValue("quantity", 2d);
        state.SetValue("approver", "Ada");
        Assert.Equal("Ada", state.GetValue("approver"));

        // The total drops below the threshold, hiding the approver — through a computed value, which is
        // the only path that used to skip the sweep.
        state.SetValue("quantity", 1d);

        Assert.Null(state.GetValue("approver"));
    }

    [Fact]
    public void A_change_to_a_computed_value_is_reported_through_FieldChanged()
    {
        var form = BlazorFormBuilder.Create()
            .Text("first")
            .Text("full", f => f.Computed(ctx => ctx.Sibling("first"), "first"))
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        var changed = new List<string>();
        state.FieldChanged += changed.Add;

        state.SetValue("first", "Ada");

        Assert.Contains("first", changed);
        Assert.Contains("full", changed);
    }

    [Fact]
    public async Task Options_that_depend_on_a_computed_value_reload_when_it_changes()
    {
        var loads = 0;
        var form = BlazorFormBuilder.Create()
            .Integer("a")
            .Integer("doubled", f => f.Computed(ctx => Convert.ToInt32(ctx.Sibling("a") ?? 0) * 2, "a"))
            .Select("tier", f => f.OptionsFrom(_ =>
            {
                loads++;
                return ValueTask.FromResult<IReadOnlyList<BlazorFormSelectOption>>(
                    [new BlazorFormSelectOption("x", "X")]);
            }, "doubled"))
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        var tier = form.FindField("tier")!;

        await state.EnsureOptionsAsync(tier, "tier");
        Assert.Equal(1, loads);

        state.SetValue("a", 5);
        await state.EnsureOptionsAsync(tier, "tier");

        Assert.Equal(2, loads);
    }
}
