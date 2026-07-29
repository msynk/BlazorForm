using FluentValidation;

namespace BlazorForm.Tests;

public class TotalsModel
{
    public int Quantity { get; set; } = 2;
    public decimal UnitPrice { get; set; } = 5;
    public decimal Total { get; set; }
}

/// <summary>
/// Dirty tracking answers "has the user changed anything?", which is what an undo button, an unsaved-
/// changes prompt and a disabled save button all read. Anything the form does to itself must not count.
/// </summary>
public class DirtyTrackingTests
{
    [Fact]
    public void A_form_with_computed_fields_starts_clean()
    {
        var form = BlazorFormBuilder.For<TotalsModel>()
            .Field(x => x.Quantity)
            .Field(x => x.UnitPrice)
            .Computed(x => x.Total, m => m.Quantity * m.UnitPrice,
                dependsOn: [nameof(TotalsModel.Quantity), nameof(TotalsModel.UnitPrice)])
            .Build();

        var model = new TotalsModel();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        // The total was seeded by the form, not typed by anyone.
        Assert.Equal(10m, model.Total);
        Assert.False(state.IsFormDirty);
    }

    [Fact]
    public void Editing_a_field_makes_the_form_dirty()
    {
        var form = BlazorFormBuilder.For<TotalsModel>().Field(x => x.Quantity).Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(new TotalsModel()));

        state.SetValue("Quantity", 9);

        Assert.True(state.IsFormDirty);
    }

    [Fact]
    public void Clearing_a_hidden_field_counts_as_a_change()
    {
        // The user did not type in the box, but the data they are about to submit is not what it was.
        var form = BlazorFormBuilder.For<SignupModel>()
            .Field(x => x.IsBusiness)
            .Field(x => x.CompanyName, f => f
                .VisibleWhen(nameof(SignupModel.IsBusiness), BlazorFormConditionOperator.IsTrue)
                .ClearOnHide())
            .Build();

        var model = new SignupModel { IsBusiness = true, CompanyName = "Acme" };
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));

        state.SetValueQuietly("IsBusiness", false);

        Assert.Null(model.CompanyName);
        Assert.True(state.IsDirty("CompanyName"));
    }
}

public class ValidityReportingTests
{
    [Fact]
    public async Task IsValid_is_only_a_verdict_once_validation_has_run()
    {
        var form = BlazorFormBuilder.For<SignupModel>().Field(x => x.Email, f => f.Required()).Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(new SignupModel()));

        // No messages yet, so IsValid reports true even though Email is empty and required. That is
        // what HasValidated is for.
        Assert.True(state.IsValid);
        Assert.False(state.HasValidated);

        await state.ValidateAsync();

        Assert.True(state.HasValidated);
        Assert.False(state.IsValid);
    }

    [Fact]
    public async Task Reset_puts_the_verdict_back_to_unknown()
    {
        var form = BlazorFormBuilder.For<SignupModel>().Field(x => x.Email, f => f.Required()).Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(new SignupModel()));

        await state.ValidateAsync();
        state.Reset();

        Assert.False(state.HasValidated);
        Assert.True(state.IsValid);
    }
}

public class SeedMinItemsModel
{
    public List<LineItem> Lines { get; set; } = [];
}

public class SeedMinItemsTests
{
    private static BlazorFormDefinition Schema(bool seed) => BlazorFormBuilder.For<SeedMinItemsModel>()
        .Array(x => x.Lines, line => line.Field(l => l.Product),
            f =>
            {
                f.Items(min: 1);
                if (seed) f.SeedMinItems();
            })
        .Build();

    [Fact]
    public void An_opted_in_repeater_opens_with_its_minimum_rows()
    {
        var model = new SeedMinItemsModel();
        var state = new BlazorFormState(Schema(seed: true), new BlazorFormModelDataAccessor(model));

        Assert.Single(model.Lines);
        // Seeding is the form setting itself up, not the user adding a row.
        Assert.False(state.IsFormDirty);
    }

    [Fact]
    public void Without_opting_in_an_empty_list_stays_empty()
    {
        // On an edit form, "this record has no lines" can be the truth.
        var model = new SeedMinItemsModel();
        _ = new BlazorFormState(Schema(seed: false), new BlazorFormModelDataAccessor(model));

        Assert.Empty(model.Lines);
    }

    [Fact]
    public void Rows_that_already_exist_are_not_topped_up_past_the_minimum()
    {
        var model = new SeedMinItemsModel { Lines = { new LineItem { Product = "Widget" } } };
        _ = new BlazorFormState(Schema(seed: true), new BlazorFormModelDataAccessor(model));

        Assert.Single(model.Lines);
        Assert.Equal("Widget", model.Lines[0].Product);
    }
}

public sealed class SignupFluentValidator : AbstractValidator<SignupModel>
{
    public SignupFluentValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        // Deliberately unconditional: the validator knows nothing about the form's visibility rules.
        RuleFor(x => x.CompanyName).NotEmpty().WithMessage("Company name is required.");
    }
}

public class ExternalValidatorVisibilityTests
{
    private static BlazorFormState Form(SignupModel model)
    {
        var schema = BlazorFormBuilder.For<SignupModel>()
            .Field(x => x.Email)
            .Field(x => x.IsBusiness)
            .Field(x => x.CompanyName, f => f
                .VisibleWhen(nameof(SignupModel.IsBusiness), BlazorFormConditionOperator.IsTrue))
            .Build();

        return new BlazorFormState(schema, new BlazorFormModelDataAccessor(model))
            .UseFluentValidation(new SignupFluentValidator());
    }

    [Fact]
    public async Task An_external_rule_on_a_hidden_field_does_not_block_the_form()
    {
        // FluentValidation sees the whole model and knows nothing about the form's conditions. Left
        // alone, its rule would refuse the submit and point at a control that is not on the page.
        var state = Form(new SignupModel { Email = "a@b.c" });

        await state.ValidateAsync();

        Assert.Empty(state.MessagesFor("CompanyName"));
        Assert.True(state.IsValid);
    }

    [Fact]
    public async Task The_same_rule_applies_once_the_field_is_shown()
    {
        var state = Form(new SignupModel { Email = "a@b.c", IsBusiness = true });

        await state.ValidateAsync();

        Assert.Single(state.MessagesFor("CompanyName"));
    }
}
