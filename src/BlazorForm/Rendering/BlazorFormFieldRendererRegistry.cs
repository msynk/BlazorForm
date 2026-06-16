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
        Register(BlazorFormFieldType.Date, typeof(BlazorFormDateInput));
        Register(BlazorFormFieldType.DateTime, typeof(BlazorFormDateInput));
        Register(BlazorFormFieldType.Time, typeof(BlazorFormDateInput));
    }

    public void Register(BlazorFormFieldType type, Type componentType) => _byType[type] = componentType;

    public void RegisterCustom(string key, Type componentType) => _byKey[key] = componentType;

    public Type Resolve(BlazorFormFieldDefinition field)
    {
        if (field.CustomRenderer is { } key && _byKey.TryGetValue(key, out var custom))
            return custom;
        if (_byType.TryGetValue(field.Type, out var type))
            return type;
        return typeof(BlazorFormTextInput);
    }
}
