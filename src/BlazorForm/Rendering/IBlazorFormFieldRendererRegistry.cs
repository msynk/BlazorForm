namespace BlazorForm;

/// <summary>
/// Resolves the Blazor component used to render each <see cref="BlazorFormFieldType"/>. Applications can
/// override built-in mappings or register renderers for custom field keys, giving the same
/// extensibility as JSON Forms' renderer set or react-jsonschema-form widgets.
/// </summary>
public interface IBlazorFormFieldRendererRegistry
{
    /// <summary>Maps a field type to the component that renders its input control.</summary>
    void Register(BlazorFormFieldType type, Type componentType);

    /// <summary>Maps a custom renderer key (see <see cref="BlazorFormFieldDefinition.CustomRenderer"/>) to a component.</summary>
    void RegisterCustom(string key, Type componentType);

    /// <summary>Resolves the component type for a field, honouring custom renderer keys first.</summary>
    Type Resolve(BlazorFormFieldDefinition field);
}
