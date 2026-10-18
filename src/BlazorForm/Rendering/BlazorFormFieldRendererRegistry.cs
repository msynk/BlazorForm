using Microsoft.AspNetCore.Components;

namespace BlazorForm;

/// <inheritdoc />
public sealed class BlazorFormFieldRendererRegistry : IBlazorFormFieldRendererRegistry
{
    private readonly Dictionary<BlazorFormFieldType, Type> _byType = new();
    private readonly Dictionary<string, Type> _byKey = new(StringComparer.OrdinalIgnoreCase);

    public BlazorFormFieldRendererRegistry()
    {
        // Built-in, dependency-free HTML renderers.
        Register(BlazorFormFieldType.Text, typeof(BlazorFormTextInput));
        Register(BlazorFormFieldType.Email, typeof(BlazorFormTextInput));
        Register(BlazorFormFieldType.Password, typeof(BlazorFormTextInput));
        Register(BlazorFormFieldType.Url, typeof(BlazorFormTextInput));
        Register(BlazorFormFieldType.Tel, typeof(BlazorFormTextInput));
        Register(BlazorFormFieldType.Search, typeof(BlazorFormTextInput));
        Register(BlazorFormFieldType.Color, typeof(BlazorFormTextInput));
        Register(BlazorFormFieldType.Hidden, typeof(BlazorFormTextInput));
        Register(BlazorFormFieldType.TextArea, typeof(BlazorFormTextAreaInput));
        Register(BlazorFormFieldType.Integer, typeof(BlazorFormNumberInput));
        Register(BlazorFormFieldType.Number, typeof(BlazorFormNumberInput));
        Register(BlazorFormFieldType.Range, typeof(BlazorFormRangeInput));
        Register(BlazorFormFieldType.Checkbox, typeof(BlazorFormCheckboxInput));
        Register(BlazorFormFieldType.Select, typeof(BlazorFormSelectInput));
        Register(BlazorFormFieldType.MultiSelect, typeof(BlazorFormMultiSelectInput));
        Register(BlazorFormFieldType.Radio, typeof(BlazorFormRadioInput));
        Register(BlazorFormFieldType.Combobox, typeof(BlazorFormComboboxInput));
        Register(BlazorFormFieldType.Tags, typeof(BlazorFormTagsInput));
        Register(BlazorFormFieldType.Date, typeof(BlazorFormDateInput));
        Register(BlazorFormFieldType.DateTime, typeof(BlazorFormDateInput));
        Register(BlazorFormFieldType.Time, typeof(BlazorFormDateInput));
        Register(BlazorFormFieldType.File, typeof(BlazorFormFileInput));
        Register(BlazorFormFieldType.Static, typeof(BlazorFormStaticContent));
    }

    public void Register(BlazorFormFieldType type, Type componentType)
    {
        EnsureComponent(componentType);
        _byType[type] = componentType;
    }

    public void RegisterCustom(string key, Type componentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        EnsureComponent(componentType);
        _byKey[key] = componentType;
    }

    public bool TryResolve(BlazorFormFieldDefinition field, out Type componentType)
    {
        // An explicit renderer key always wins, so a field can opt out of its type's default widget.
        if (field.CustomRenderer is { Length: > 0 } key && _byKey.TryGetValue(key, out var custom))
        {
            componentType = custom;
            return true;
        }
        return _byType.TryGetValue(field.Type, out componentType!);
    }

    public Type Resolve(BlazorFormFieldDefinition field)
    {
        if (TryResolve(field, out var componentType)) return componentType;

        if (field.Type == BlazorFormFieldType.Custom)
        {
            // No key at all is a different mistake from an unregistered one, and naming an empty key in
            // the registration hint sends the developer looking for a typo that is not there. The usual
            // way to arrive here is a property no built-in control can edit — a byte[] or a Stream.
            if (string.IsNullOrEmpty(field.CustomRenderer))
                throw new InvalidOperationException(
                    $"Field '{field.Name}' is a custom field with no renderer key, so no component can render it. " +
                    "No built-in control edits a value of this kind (a byte[] or a Stream, say): give it a renderer with " +
                    ".CustomRenderer(\"key\") and register that key with services.AddBlazorForm(r => r.RegisterCustom(\"key\", typeof(MyComponent))).");

            throw new InvalidOperationException(
                $"Field '{field.Name}' uses the custom renderer key '{field.CustomRenderer}', which is not registered. " +
                "Register it with services.AddBlazorForm(r => r.RegisterCustom(\"" + field.CustomRenderer + "\", typeof(MyComponent))).");
        }

        // Any other unmapped type still renders something usable rather than nothing.
        return typeof(BlazorFormTextInput);
    }

    private static void EnsureComponent(Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        if (!typeof(IComponent).IsAssignableFrom(componentType))
            throw new ArgumentException($"'{componentType}' is not a Blazor component.", nameof(componentType));
    }
}
