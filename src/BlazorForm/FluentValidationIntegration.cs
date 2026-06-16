using FluentValidation;
using FluentValidation.Results;
using BlazorForm.Core.Schema;
using BlazorForm.Core.State;
using BlazorForm.Core.Validation;
using FvSeverity = FluentValidation.Severity;
using FvContext = global::FluentValidation.ValidationContext<object>;

namespace BlazorForm.FluentValidation;

/// <summary>
/// Bridges FluentValidation into BlazorForm. FluentValidation reports failures with property paths
/// (e.g. <c>Address.City</c>, <c>Items[0].Product</c>) that already match BlazorForm field paths, so
/// results map directly onto fields.
/// </summary>
public static class FluentValidationIntegration
{
    /// <summary>
    /// Registers a concrete validator instance as the form's external validator.
    /// </summary>
    public static FormState UseFluentValidation<TModel>(this FormState state, IValidator<TModel> validator)
        where TModel : class
    {
        state.ExternalValidator = (_, data, _) =>
        {
            if (data.Root is not TModel model)
                return new ValueTask<IReadOnlyList<ValidationMessage>>(Array.Empty<ValidationMessage>());
            return RunAsync(validator, new global::FluentValidation.ValidationContext<TModel>(model));
        };
        return state;
    }

    /// <summary>
    /// Resolves <c>IValidator&lt;TModel&gt;</c> for the form's <see cref="FormDefinition.ModelType"/>
    /// from the service provider and registers it as the external validator.
    /// </summary>
    public static FormState UseFluentValidation(this FormState state)
    {
        state.ExternalValidator = async (form, data, services) =>
        {
            if (form.ModelType is null || data.Root is null || services is null)
                return Array.Empty<ValidationMessage>();

            var validatorType = typeof(IValidator<>).MakeGenericType(form.ModelType);
            if (services.GetService(validatorType) is not IValidator validator)
                return Array.Empty<ValidationMessage>();

            var context = new FvContext(data.Root);
            var result = await validator.ValidateAsync(context);
            return Map(result);
        };
        return state;
    }

    private static async ValueTask<IReadOnlyList<ValidationMessage>> RunAsync<TModel>(
        IValidator<TModel> validator, global::FluentValidation.ValidationContext<TModel> context)
    {
        var result = await validator.ValidateAsync(context);
        return Map(result);
    }

    private static IReadOnlyList<ValidationMessage> Map(ValidationResult result)
    {
        if (result.IsValid) return Array.Empty<ValidationMessage>();
        var messages = new List<ValidationMessage>(result.Errors.Count);
        foreach (var failure in result.Errors)
            messages.Add(new ValidationMessage(failure.PropertyName, failure.ErrorMessage, MapSeverity(failure.Severity)));
        return messages;
    }

    private static ValidationSeverity MapSeverity(FvSeverity severity) => severity switch
    {
        FvSeverity.Error => ValidationSeverity.Error,
        FvSeverity.Warning => ValidationSeverity.Warning,
        FvSeverity.Info => ValidationSeverity.Info,
        _ => ValidationSeverity.Error
    };
}
