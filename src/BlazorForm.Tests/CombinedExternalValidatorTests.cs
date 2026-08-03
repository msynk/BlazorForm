using FluentValidation;

namespace BlazorForm.Tests;

/// <summary>
/// A form has one external-validator slot, and every integration writes to it. Wiring up
/// FluentValidation <em>and</em> the model's own <c>IValidatableObject</c> is an entirely reasonable
/// thing to want — they cover different rules — and it used to mean whichever line came second
/// silently threw the first away, leaving a form with half its validation and nothing to say why.
/// </summary>
public class CombinedExternalValidatorTests
{
    private sealed class BookingFluentValidator : AbstractValidator<BookingRequest>
    {
        public BookingFluentValidator()
            => RuleFor(x => x.Reference).NotEmpty().WithMessage("A reference is required.");
    }

    private static BookingRequest Bad() => new()
    {
        Reference = "",                          // the FluentValidation rule
        Start = new DateOnly(2026, 5, 10),
        End = new DateOnly(2026, 5, 1)           // the IValidatableObject rule
    };

    private static BlazorFormState State(BookingRequest model)
        => new(BlazorFormSchemaGenerator.Generate<BookingRequest>(), new BlazorFormModelDataAccessor(model));

    [Fact]
    public async Task Both_layers_run_whichever_order_they_are_wired_in()
    {
        var first = State(Bad()).UseDataAnnotations().UseFluentValidation(new BookingFluentValidator());
        Assert.False(await first.ValidateAsync());
        Assert.Contains(first.MessagesFor("End"), m => m.Message.Contains("end date", StringComparison.Ordinal));
        Assert.Contains(first.MessagesFor("Reference"), m => m.Message.Contains("reference is required", StringComparison.OrdinalIgnoreCase));

        var second = State(Bad()).UseFluentValidation(new BookingFluentValidator()).UseDataAnnotations();
        Assert.False(await second.ValidateAsync());
        Assert.Contains(second.MessagesFor("End"), m => m.Message.Contains("end date", StringComparison.Ordinal));
        Assert.Contains(second.MessagesFor("Reference"), m => m.Message.Contains("reference is required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task The_same_complaint_from_both_layers_is_read_once()
    {
        // Two layers can legitimately report the same problem on the same field in the same words.
        var state = State(Bad());
        state.ExternalValidator = state.ExternalValidator
            .CombineWith((_, _, _) => new ValueTask<IReadOnlyList<BlazorFormValidationMessage>>(
                (IReadOnlyList<BlazorFormValidationMessage>)[new BlazorFormValidationMessage("Reference", "Say it once.")]))
            .CombineWith((_, _, _) => new ValueTask<IReadOnlyList<BlazorFormValidationMessage>>(
                (IReadOnlyList<BlazorFormValidationMessage>)[new BlazorFormValidationMessage("Reference", "Say it once.")]));

        await state.ValidateAsync();

        Assert.Single(state.MessagesFor("Reference"), m => m.Message == "Say it once.");
    }

    [Fact]
    public void Combining_with_nothing_hands_the_validator_straight_back()
    {
        BlazorFormExternalValidator only = (_, _, _) =>
            new ValueTask<IReadOnlyList<BlazorFormValidationMessage>>(Array.Empty<BlazorFormValidationMessage>());

        Assert.Same(only, ((BlazorFormExternalValidator?)null).CombineWith(only));
    }
}
