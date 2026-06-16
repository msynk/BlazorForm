using global::FluentValidation;
using global::FluentValidation.Results;
using FvSeverity = global::FluentValidation.Severity;
using FvContext = global::FluentValidation.ValidationContext<object>;

namespace BlazorForm;

/// <summary>
/// Bridges FluentValidation into BlazorForm. FluentValidation reports failures with property paths
/// (e.g. <c>Address.City</c>, <c>Items[0].Product</c>) that already match BlazorForm field paths, so
/// results map directly onto fields.
/// </summary>
public static class BlazorFormFluentValidationIntegration
{
    /// <summary>
    /// Registers a concrete validator instance as the form's external validator.
    /// </summary>
    public static BlazorFormState UseFluentValidation<TModel>(this BlazorFormState state, IValidator<TModel> validator)
        where TModel : class
    {
        state.ExternalValidator = (_, data, _) =>
        {
            if (data.Root is not TModel model)
                return new ValueTask<IReadOnlyList<BlazorFormValidationMessage>>(Array.Empty<BlazorFormValidationMessage>());
            return RunAsync(validator, new global::FluentValidation.ValidationContext<TModel>(model));
        };
        return state;
    }

    /// <summary>
    /// Resolves <c>IValidator&lt;TModel&gt;</c> for the form's <see cref="BlazorFormDefinition.ModelType"/>
    /// from the service provider and registers it as the external validator.
    /// </summary>
    public static BlazorFormState UseFluentValidation(this BlazorFormState state)
    {
        state.ExternalValidator = async (form, data, services) =>
        {
            if (form.ModelType is null || data.Root is null || services is null)
                return Array.Empty<BlazorFormValidationMessage>();

            var validatorType = typeof(IValidator<>).MakeGenericType(form.ModelType);
            if (services.GetService(validatorType) is not IValidator validator)
                return Array.Empty<BlazorFormValidationMessage>();

            var context = new FvContext(data.Root);
            var result = await validator.ValidateAsync(context);
            return Map(result);
        };
        return state;
    }

    private static async ValueTask<IReadOnlyList<BlazorFormValidationMessage>> RunAsync<TModel>(
        IValidator<TModel> validator, global::FluentValidation.ValidationContext<TModel> context)
    {
        var result = await validator.ValidateAsync(context);
        return Map(result);
    }

    private static IReadOnlyList<BlazorFormValidationMessage> Map(ValidationResult result)
    {
        if (result.IsValid) return Array.Empty<BlazorFormValidationMessage>();
        var messages = new List<BlazorFormValidationMessage>(result.Errors.Count);
        foreach (var failure in result.Errors)
            messages.Add(new BlazorFormValidationMessage(failure.PropertyName, failure.ErrorMessage, MapSeverity(failure.Severity)));
        return messages;
    }

    private static BlazorFormValidationSeverity MapSeverity(FvSeverity severity) => severity switch
    {
        FvSeverity.Error => BlazorFormValidationSeverity.Error,
        FvSeverity.Warning => BlazorFormValidationSeverity.Warning,
        FvSeverity.Info => BlazorFormValidationSeverity.Info,
        _ => BlazorFormValidationSeverity.Error
    };
}
