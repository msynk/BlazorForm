namespace BlazorForm;

/// <summary>Context passed to a validation rule.</summary>
public sealed class BlazorFormValidationContext
{
    public BlazorFormValidationContext(string fieldPath, object? value, IBlazorFormDataReader data, IServiceProvider? services = null)
    {
        FieldPath = fieldPath;
        Value = value;
        Data = data;
        Services = services;
    }

    /// <summary>The path of the field being validated.</summary>
    public string FieldPath { get; }

    /// <summary>The current value of the field being validated.</summary>
    public object? Value { get; }

    /// <summary>Read access to the whole form, for cross-field rules.</summary>
    public IBlazorFormDataReader Data { get; }

    /// <summary>Optional service provider for rules that need DI (e.g. async uniqueness checks).</summary>
    public IServiceProvider? Services { get; }
}
