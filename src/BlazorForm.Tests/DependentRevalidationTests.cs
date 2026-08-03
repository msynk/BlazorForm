using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorForm.Tests;

/// <summary>
/// A cross-field rule lives on one of the two fields it compares, so only one of the two changes ever
/// runs it. Correcting the other one leaves a verdict on screen that is no longer true — the
/// confirm-password complaint that survives the password being fixed. React Hook Form's <c>deps</c>
/// and TanStack Form's <c>onChangeListenTo</c> exist for this; <c>RevalidateOn</c> is ours.
/// </summary>
public class DependentRevalidationTests
{
    private static BlazorFormDefinition Passwords() => BlazorFormBuilder.Create()
        .Text("Password", f => f.AsPassword().Required())
        .Text("Confirm", f => f.AsPassword().MatchesField("Password", "Password"))
        .Build();

    private static BlazorFormState Form(BlazorFormDefinition form)
        => new(form, new BlazorFormDictionaryDataAccessor());

    [Fact]
    public void MatchesField_registers_the_path_it_reads()
    {
        var confirm = Passwords().FindByPath("Confirm")!;
        Assert.Contains("Password", confirm.RevalidateOn);
    }

    [Fact]
    public async Task Correcting_the_other_field_clears_a_stale_mismatch()
    {
        var state = Form(Passwords());
        state.SetValue("Password", "hunter2");
        state.SetValue("Confirm", "hunter3");
        await state.ValidateFieldAsync("Confirm");
        Assert.NotEmpty(state.MessagesFor("Confirm"));

        // The user goes back and fixes the *password* to agree with what they typed. Nothing touches
        // the confirmation box, so nothing but this would revalidate it.
        state.SetValue("Password", "hunter3");
        await state.ValidateDependentsAsync("Password");

        Assert.Empty(state.MessagesFor("Confirm"));
    }

    [Fact]
    public async Task Breaking_the_match_from_the_other_side_is_reported()
    {
        var state = Form(Passwords());
        state.SetValue("Password", "hunter2");
        state.SetValue("Confirm", "hunter2");
        await state.ValidateFieldAsync("Confirm");
        Assert.Empty(state.MessagesFor("Confirm"));

        state.SetValue("Password", "something-else");
        await state.ValidateDependentsAsync("Password");

        Assert.NotEmpty(state.MessagesFor("Confirm"));
    }

    [Fact]
    public async Task A_dependent_nobody_has_reached_is_left_alone()
    {
        // The point is to correct a verdict already on screen, never to bring one forward: typing a
        // password must not make an untouched confirmation box start showing errors.
        var state = Form(Passwords());
        state.SetValueQuietly("Password", "hunter2");
        await state.ValidateDependentsAsync("Password");

        Assert.Empty(state.MessagesFor("Confirm"));
        Assert.False(state.IsTouched("Confirm"));
    }

    [Fact]
    public async Task A_dependent_is_revalidated_once_the_form_has_been_submitted()
    {
        var state = Form(Passwords());
        state.SetValue("Password", "hunter2");
        state.SetValue("Confirm", "nope");
        await state.SubmitAsync();
        Assert.NotEmpty(state.MessagesFor("Confirm"));

        state.SetValue("Password", "nope");
        await state.ValidateDependentsAsync("Password");

        Assert.Empty(state.MessagesFor("Confirm"));
    }

    [Fact]
    public async Task A_row_revalidates_against_its_own_sibling_not_the_first_row()
    {
        // Paths resolve against the object that owns the field, exactly as conditions and computed
        // dependencies do, so row 2's rule reads row 2's value.
        var form = BlazorFormBuilder.Create()
            .ArrayOf("Rows", BlazorFormFieldType.Object, a => a.Items(min: 0))
            .Build();
        var rows = form.FindByPath("Rows")!;
        rows.ItemTemplate = new BlazorFormFieldDefinition("item", BlazorFormFieldType.Object)
        {
            Children =
            {
                new BlazorFormFieldDefinition("Email", BlazorFormFieldType.Email),
                new BlazorFormFieldDefinition("ConfirmEmail", BlazorFormFieldType.Email)
                {
                    Validators = { new BlazorFormCompareRule("Email", "Email") },
                    RevalidateOn = { "Email" }
                }
            }
        };

        var state = Form(form);
        state.AddArrayItem(rows, "Rows");
        state.AddArrayItem(rows, "Rows");

        state.SetValue("Rows[0].Email", "a@example.com");
        state.SetValue("Rows[0].ConfirmEmail", "a@example.com");
        state.SetValue("Rows[1].Email", "b@example.com");
        state.SetValue("Rows[1].ConfirmEmail", "typo@example.com");
        await state.ValidateFieldAsync("Rows[1].ConfirmEmail");
        Assert.NotEmpty(state.MessagesFor("Rows[1].ConfirmEmail"));

        state.SetValue("Rows[1].Email", "typo@example.com");
        await state.ValidateDependentsAsync("Rows[1].Email");

        Assert.Empty(state.MessagesFor("Rows[1].ConfirmEmail"));
        // Row 0 was never in question and was not disturbed.
        Assert.Empty(state.MessagesFor("Rows[0].ConfirmEmail"));
    }

    [Fact]
    public async Task A_schema_with_no_dependencies_pays_nothing()
    {
        var form = BlazorFormBuilder.Create().Text("a", f => f.Required()).Build();
        var state = Form(form);
        state.SetValue("a", "");
        await state.ValidateFieldAsync("a");

        // A no-op, but it must not throw or disturb what is already recorded.
        await state.ValidateDependentsAsync("a");
        Assert.NotEmpty(state.MessagesFor("a"));
    }

    [Fact]
    public void The_generator_wires_up_a_Compare_attribute()
    {
        var form = BlazorFormSchemaGenerator.Generate<SignupModel>();
        Assert.Contains("Password", form.FindByPath("ConfirmPassword")!.RevalidateOn);
    }

    [Fact]
    public void A_comparison_that_never_revalidates_is_reported()
    {
        var form = BlazorFormBuilder.Create()
            .Text("a")
            .Text("b")
            .Build();
        // Added directly rather than through MatchesField, which wires itself up.
        form.FindByPath("b")!.Validators.Add(new BlazorFormCompareRule("a"));

        Assert.Contains(form.Validate(),
            d => d.Message.Contains("does not revalidate", StringComparison.Ordinal));
        Assert.DoesNotContain(Passwords().Validate(),
            d => d.Message.Contains("does not revalidate", StringComparison.Ordinal));
    }

    [Fact]
    public void RevalidateOn_survives_a_clone_and_a_json_round_trip()
    {
        var form = Passwords();
        Assert.Contains("Password", form.Clone().FindByPath("Confirm")!.RevalidateOn);

        var json = BlazorFormJsonSchemaExporter.Export(form);
        Assert.Contains("x-revalidateOn", json, StringComparison.Ordinal);
        Assert.Contains("Password", BlazorFormJsonSchemaImporter.Import(json).FindByPath("Confirm")!.RevalidateOn);
    }

    [Fact]
    public void A_path_that_names_nothing_is_reported()
    {
        var form = BlazorFormBuilder.Create()
            .Text("a", f => f.RevalidateOn("nowhere"))
            .Build();

        Assert.Contains(form.Validate(), d => d.Message.Contains("RevalidateOn", StringComparison.Ordinal));
    }
}

/// <summary>The same thing again, through the real controls — the path an actual user takes.</summary>
public class DependentRevalidationRenderingTests : BunitContext
{
    public DependentRevalidationRenderingTests() => Services.AddBlazorForm();

    [Fact]
    public async Task Adding_a_repeater_row_revalidates_what_depends_on_the_list()
    {
        // A repeater operation changes the value of the field the list binds to, but a repeater has no
        // field context to route through — so a rule elsewhere that counts the rows was judged once and
        // never again.
        var form = BlazorFormBuilder.Create()
            .Integer("Seats", f => f
                .RevalidateOn("Guests")
                .Must(ctx => ctx.Value is not int seats
                             || ctx.Data.GetValue("Guests") is not System.Collections.ICollection guests
                             || seats >= guests.Count,
                      "Not enough seats for the guests listed."))
            .ArrayOf("Guests", BlazorFormFieldType.Text)
            .Build();

        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, form));
        var state = cut.Instance.Form;
        var guests = form.FindByPath("Guests")!;

        state.SetValue("Seats", 1);
        state.AddArrayItem(guests, "Guests");
        await state.ValidateFieldAsync("Seats");
        Assert.Empty(state.MessagesFor("Seats"));

        // Through the button, so the repeater's own wiring is what is under test.
        cut.Find(".ff-btn--add").Click();

        Assert.NotEmpty(state.MessagesFor("Seats"));
    }

    [Fact]
    public async Task Editing_the_password_control_clears_the_confirmation_error()
    {
        var form = BlazorFormBuilder.Create()
            .Text("Password", f => f.Label("Password").AsPassword())
            .Text("Confirm", f => f.Label("Confirm").AsPassword().MatchesField("Password", "Password"))
            .Build();

        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, form));
        var state = cut.Instance.Form;

        cut.Find("input#ff_Password").Change("hunter2");
        cut.Find("input#ff_Confirm").Change("hunter3");
        await cut.Find("input#ff_Confirm").BlurAsync(new());
        Assert.NotEmpty(state.MessagesFor("Confirm"));

        cut.Find("input#ff_Password").Change("hunter3");

        Assert.Empty(state.MessagesFor("Confirm"));
    }
}
