namespace BlazorForm;

/// <summary>Context passed to a validation rule.</summary>
public sealed class BlazorFormValidationContext
{
    public BlazorFormValidationContext(
        string fieldPath,
        object? value,
        IBlazorFormDataReader data,
        IServiceProvider? services = null,
        BlazorFormFieldDefinition? field = null)
    {
        FieldPath = fieldPath;
        Value = value;
        Data = data;
        Services = services;
        Field = field;
    }

    /// <summary>The path of the field being validated.</summary>
    public string FieldPath { get; }

    /// <summary>The current value of the field being validated.</summary>
    public object? Value { get; }

    /// <summary>Read access to the whole form, for cross-field rules.</summary>
    public IBlazorFormDataReader Data { get; }

    /// <summary>Optional service provider for rules that need DI (e.g. async uniqueness checks).</summary>
    public IServiceProvider? Services { get; }

    /// <summary>The definition of the field being validated, when the caller supplied it.</summary>
    public BlazorFormFieldDefinition? Field { get; }

    /// <summary>The field's label, falling back to its path — for messages that need to name the field.</summary>
    public string DisplayName => Field?.Label ?? FieldPath;

    /// <summary>
    /// Resolves the text of a built-in message (see <see cref="BlazorFormMessageKeys"/>) through the
    /// <see cref="IBlazorFormMessageProvider"/> registered in <see cref="Services"/>, falling back to
    /// the English defaults when none is registered.
    /// </summary>
    public string Message(string key, params object?[] args)
        => (Services?.GetService(typeof(IBlazorFormMessageProvider)) as IBlazorFormMessageProvider
            ?? BlazorFormDefaultMessageProvider.Instance).Get(key, args);
}
