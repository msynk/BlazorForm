namespace BlazorForm;

/// <summary>
/// Optional external validator (e.g. FluentValidation) invoked alongside the schema's built-in rules.
/// </summary>
public delegate ValueTask<IReadOnlyList<BlazorFormValidationMessage>> BlazorFormExternalValidator(
    BlazorFormDefinition form, IBlazorFormDataReader data, IServiceProvider? services);
