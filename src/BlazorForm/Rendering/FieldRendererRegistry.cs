using BlazorForm.Components.Inputs;
using BlazorForm.Core.Schema;

namespace BlazorForm.Rendering;

/// <summary>
/// Resolves the Blazor component used to render each <see cref="FieldType"/>. Applications can
/// override built-in mappings or register renderers for custom field keys, giving the same
/// extensibility as JSON Forms' renderer set or react-jsonschema-form widgets.
/// </summary>
public interface IFieldRendererRegistry
{
    /// <summary>Maps a field type to the component that renders its input control.</summary>
    void Register(FieldType type, Type componentType);

    /// <summary>Maps a custom renderer key (see <see cref="FieldDefinition.CustomRenderer"/>) to a component.</summary>
    void RegisterCustom(string key, Type componentType);

    /// <summary>Resolves the component type for a field, honouring custom renderer keys first.</summary>
    Type Resolve(FieldDefinition field);
}

/// <inheritdoc />
public sealed class FieldRendererRegistry : IFieldRendererRegistry
{
    private readonly Dictionary<FieldType, Type> _byType = new();
    private readonly Dictionary<string, Type> _byKey = new(StringComparer.OrdinalIgnoreCase);

    public FieldRendererRegistry()
    {
        // Built-in, dependency-free HTML renderers.
        Register(FieldType.Text, typeof(TextInput));
        Register(FieldType.Email, typeof(TextInput));
        Register(FieldType.Password, typeof(TextInput));
        Register(FieldType.Url, typeof(TextInput));
        Register(FieldType.Tel, typeof(TextInput));
        Register(FieldType.Color, typeof(TextInput));
        Register(FieldType.Hidden, typeof(TextInput));
        Register(FieldType.TextArea, typeof(TextAreaInput));
        Register(FieldType.Integer, typeof(NumberInput));
        Register(FieldType.Number, typeof(NumberInput));
        Register(FieldType.Range, typeof(RangeInput));
        Register(FieldType.Checkbox, typeof(CheckboxInput));
        Register(FieldType.Select, typeof(SelectInput));
        Register(FieldType.MultiSelect, typeof(MultiSelectInput));
        Register(FieldType.Radio, typeof(RadioInput));
        Register(FieldType.Date, typeof(DateInput));
        Register(FieldType.DateTime, typeof(DateInput));
        Register(FieldType.Time, typeof(DateInput));
    }

    public void Register(FieldType type, Type componentType) => _byType[type] = componentType;

    public void RegisterCustom(string key, Type componentType) => _byKey[key] = componentType;

    public Type Resolve(FieldDefinition field)
    {
        if (field.CustomRenderer is { } key && _byKey.TryGetValue(key, out var custom))
            return custom;
        if (_byType.TryGetValue(field.Type, out var type))
            return type;
        return typeof(TextInput);
    }
}
