using Microsoft.AspNetCore.Components;

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

    /// <summary>
    /// Resolves the component type for a field, honouring custom renderer keys first.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The field is <see cref="BlazorFormFieldType.Custom"/> but its
    /// <see cref="BlazorFormFieldDefinition.CustomRenderer"/> key has no registered component. Failing
    /// loudly beats silently rendering a text box the developer never asked for.
    /// </exception>
    Type Resolve(BlazorFormFieldDefinition field);

    /// <summary>Resolves the component type for a field, returning false instead of throwing when none is registered.</summary>
    bool TryResolve(BlazorFormFieldDefinition field, out Type componentType);
}

/// <summary>Strongly-typed conveniences over <see cref="IBlazorFormFieldRendererRegistry"/>.</summary>
public static class BlazorFormFieldRendererRegistryExtensions
{
    /// <summary>Maps a field type to <typeparamref name="TComponent"/>.</summary>
    public static IBlazorFormFieldRendererRegistry Register<TComponent>(
        this IBlazorFormFieldRendererRegistry registry, BlazorFormFieldType type)
        where TComponent : IComponent
    {
        registry.Register(type, typeof(TComponent));
        return registry;
    }

    /// <summary>Maps a custom renderer key to <typeparamref name="TComponent"/>.</summary>
    public static IBlazorFormFieldRendererRegistry RegisterCustom<TComponent>(
        this IBlazorFormFieldRendererRegistry registry, string key)
        where TComponent : IComponent
    {
        registry.RegisterCustom(key, typeof(TComponent));
        return registry;
    }
}
