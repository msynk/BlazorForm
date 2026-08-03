using System.ComponentModel.DataAnnotations;
using Bunit;

namespace BlazorForm.Tests;

/// <summary>
/// A length limit on a field that holds several strings applies to each of them. Reading the value as
/// a string and finding a list made the rule quietly do nothing — the same shape of bug as
/// <c>[MinLength]</c> on a collection being mapped to the string rule.
/// </summary>
public class TagLengthRuleTests
{
    private static BlazorFormState Form(int min = 0, int max = 10)
    {
        var builder = BlazorFormBuilder.Create().Tags("labels", f =>
        {
            f.AsTags();
            if (min > 0) f.MinLength(min);
            f.MaxLength(max);
        });
        return new BlazorFormState(builder.Build(), new BlazorFormDictionaryDataAccessor());
    }

    [Fact]
    public async Task A_tag_over_the_limit_is_reported()
    {
        var state = Form(max: 5);
        state.SetValue("labels", new List<string> { "ok", "far too long" });

        Assert.False(await state.ValidateAsync());
        Assert.NotEmpty(state.MessagesFor("labels"));
    }

    [Fact]
    public async Task Tags_within_the_limit_pass()
    {
        var state = Form(max: 5);
        state.SetValue("labels", new List<string> { "ok", "fine" });

        Assert.True(await state.ValidateAsync());
    }

    [Fact]
    public async Task A_tag_under_the_minimum_is_reported()
    {
        var state = Form(min: 3, max: 20);
        state.SetValue("labels", new List<string> { "ab" });

        Assert.False(await state.ValidateAsync());
    }

    [Fact]
    public async Task An_empty_tag_list_is_Requireds_business_not_this_rules()
    {
        var state = Form(min: 3, max: 20);

        Assert.True(await state.ValidateAsync());
    }

    [Fact]
    public async Task A_repeater_of_strings_is_untouched_by_the_change()
    {
        // Only a tag list holds several values the limit applies to individually. An array's length is
        // the collection-size rule's business, and reinterpreting it here would change what an existing
        // schema means.
        var form = BlazorFormBuilder.Create()
            .ArrayOf("items", BlazorFormFieldType.Text, f => f.MaxLength(3))
            .Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        state.SetValue("items", new List<object?> { "far too long" });

        Assert.True(await state.ValidateAsync());
    }

    [Fact]
    public async Task A_plain_string_field_still_measures_the_string()
    {
        var form = BlazorFormBuilder.Create().Text("a", f => f.MaxLength(3)).Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        state.SetValue("a", "abcd");

        Assert.False(await state.ValidateAsync());
    }
}

public class TagsFieldLabellingTests : ComponentTestBase
{
    private static BlazorFormDefinition Schema() => BlazorFormBuilder.Create()
        .Tags("labels", f => f.Label("Labels").AsTags())
        .Build();

    [Fact]
    public void The_label_is_a_span_the_group_points_at_not_a_for_attribute()
    {
        // The box a tag is typed into disappears once the list is full or the form is read-only, so a
        // `for` aimed at it would be left pointing at nothing.
        var cut = Render<BlazorFormView>(p => p.Add(x => x.Definition, Schema()));

        Assert.Empty(cut.FindAll("label[for='ff_labels']"));
        Assert.Equal("ff_labels_label", cut.Find(".ff-tags").GetAttribute("aria-labelledby"));
        Assert.Contains("Labels", cut.Find("#ff_labels_label").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void The_group_carries_the_field_id_so_the_error_summary_can_link_to_it()
    {
        var state = new BlazorFormState(Schema(), new BlazorFormDictionaryDataAccessor());
        var cut = Render<BlazorFormView>(p => p.Add(x => x.State, state));

        Assert.Equal("ff_labels", cut.Find(".ff-tags").GetAttribute("id"));
        Assert.Equal("ff_labels_entry", cut.Find(".ff-tags__entry").GetAttribute("id"));
    }
}

public class ComboboxDiagnosticTests
{
    [Fact]
    public void Two_options_with_the_same_label_are_reported()
    {
        // The entry is matched back to an option by its label, because the label is what the browser
        // puts in the box. Two of them makes the user's choice a coin toss.
        var form = BlazorFormBuilder.Create()
            .Combobox("city", f => f.Options(("par-fr", "Paris"), ("par-us", "Paris")).AsCombobox())
            .Build();

        var problems = form.Validate();

        Assert.Contains(problems, p => p.Message.Contains("labelled 'Paris'", StringComparison.Ordinal));
    }

    [Fact]
    public void Distinct_labels_are_not_reported()
    {
        var form = BlazorFormBuilder.Create()
            .Combobox("city", f => f.Options(("par", "Paris, France"), ("par-us", "Paris, Texas")).AsCombobox())
            .Build();

        Assert.DoesNotContain(form.Validate(), p => p.Message.Contains("labelled", StringComparison.Ordinal));
    }

    [Fact]
    public void A_select_with_repeated_labels_is_not_reported_because_it_matches_on_value()
    {
        var form = BlazorFormBuilder.Create()
            .Select("city", f => f.Options(("par-fr", "Paris"), ("par-us", "Paris")))
            .Build();

        Assert.DoesNotContain(form.Validate(), p => p.Message.Contains("labelled", StringComparison.Ordinal));
    }
}

public class BookingRequest : IValidatableObject
{
    [Required] public string Reference { get; set; } = "";
    public DateOnly Start { get; set; }
    public DateOnly End { get; set; }
    public int Guests { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (End < Start)
            yield return new ValidationResult("The end date must be on or after the start date.", [nameof(End)]);

        if (Guests > 4 && Reference.Length == 0)
            yield return new ValidationResult("Large bookings need a reference.", [nameof(Guests), nameof(Reference)]);

        if (Guests < 0)
            yield return new ValidationResult("This booking does not make sense.");
    }
}

/// <summary>
/// The .NET-native counterpart of the FluentValidation bridge. The generator already turns the
/// attributes it understands into field rules; what it cannot see is a custom ValidationAttribute's
/// logic, or IValidatableObject — the standard way a model says "these two properties have to agree".
/// </summary>
public class DataAnnotationsIntegrationTests
{
    private static BlazorFormState Form(BookingRequest model)
        => new BlazorFormState(BlazorFormSchemaGenerator.Generate<BookingRequest>(),
            new BlazorFormModelDataAccessor(model)).UseDataAnnotations();

    [Fact]
    public async Task IValidatableObject_failures_land_on_the_field_they_name()
    {
        var state = Form(new BookingRequest
        {
            Reference = "R1",
            Start = new DateOnly(2026, 5, 10),
            End = new DateOnly(2026, 5, 1)
        });

        Assert.False(await state.ValidateAsync());
        Assert.Contains(state.MessagesFor("End"), m => m.Message.Contains("end date", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_failure_naming_several_members_appears_under_each_of_them()
    {
        var state = Form(new BookingRequest { Guests = 8, Start = new DateOnly(2026, 1, 1), End = new DateOnly(2026, 1, 2) });

        await state.ValidateAsync();

        Assert.Contains(state.MessagesFor("Guests"), m => m.Message.Contains("reference", StringComparison.Ordinal));
        Assert.Contains(state.MessagesFor("Reference"), m => m.Message.Contains("reference", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_failure_naming_no_member_is_a_form_level_message()
    {
        var state = Form(new BookingRequest
        {
            Reference = "R1",
            Guests = -1,
            Start = new DateOnly(2026, 1, 1),
            End = new DateOnly(2026, 1, 2)
        });

        await state.ValidateAsync();

        Assert.Contains(state.MessagesFor(string.Empty), m => m.Message.Contains("does not make sense", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_valid_model_passes()
    {
        var state = Form(new BookingRequest
        {
            Reference = "R1",
            Guests = 2,
            Start = new DateOnly(2026, 1, 1),
            End = new DateOnly(2026, 1, 2)
        });

        Assert.True(await state.ValidateAsync());
    }

    [Fact]
    public async Task A_failure_on_a_field_the_schema_hides_is_dropped()
    {
        // Same rule as FluentValidation: a validator sees the whole model and knows nothing about the
        // form's conditions, so without this a hidden branch would refuse the submit and point at a
        // control that is not on the page.
        var form = BlazorFormBuilder.For<BookingRequest>()
            .Field(x => x.Reference)
            .Field(x => x.Start)
            .Field(x => x.End, f => f.VisibleWhen(_ => false))
            .Field(x => x.Guests)
            .Build();

        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(new BookingRequest
        {
            Reference = "R1",
            Start = new DateOnly(2026, 5, 10),
            End = new DateOnly(2026, 5, 1)
        })).UseDataAnnotations();

        Assert.True(await state.ValidateAsync());
    }

    [Fact]
    public async Task Cross_property_messages_appear_even_while_a_required_field_is_still_empty()
    {
        // Validator.TryValidateObject skips IValidatableObject entirely once any property attribute has
        // failed, so the user would fix the last required field, press submit again, and only then be
        // told about something new. Everything found is reported at once instead.
        var state = Form(new BookingRequest
        {
            Reference = "",
            Start = new DateOnly(2026, 5, 10),
            End = new DateOnly(2026, 5, 1)
        });

        await state.ValidateAsync();

        Assert.NotEmpty(state.MessagesFor("Reference"));
        Assert.Contains(state.MessagesFor("End"), m => m.Message.Contains("end date", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_property_attributes_are_left_to_the_generated_field_rules()
    {
        // They are already field rules, so running Validator over them again would put two
        // differently-worded copies of "required" under the same box.
        var state = Form(new BookingRequest { Start = new DateOnly(2026, 1, 1), End = new DateOnly(2026, 1, 2) });

        await state.ValidateAsync();

        Assert.Single(state.MessagesFor("Reference"));
    }

    [Fact]
    public async Task Opting_in_enforces_the_property_attributes_for_a_schema_that_never_saw_them()
    {
        // A JSON-imported schema rendered over a typed model has no generated rules at all, so the
        // model's own constraints have to come from somewhere.
        var form = BlazorFormBuilder.Create().Text("Reference").Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(new BookingRequest()))
            .UseDataAnnotations(includePropertyAttributes: true);

        Assert.False(await state.ValidateAsync());
        Assert.NotEmpty(state.MessagesFor("Reference"));
    }

    [Fact]
    public async Task A_model_with_no_IValidatableObject_is_simply_quiet()
    {
        var form = BlazorFormBuilder.For<RegistrationModel>()
            .Field(x => x.FirstName)
            .Field(x => x.Email)
            .Build();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(new RegistrationModel
        {
            FirstName = "Ada",
            Email = "ada@example.com"
        })).UseDataAnnotations();

        Assert.True(await state.ValidateAsync());
    }

    [Fact]
    public void A_null_state_is_rejected()
        => Assert.Throws<ArgumentNullException>(() => ((BlazorFormState)null!).UseDataAnnotations());
}
