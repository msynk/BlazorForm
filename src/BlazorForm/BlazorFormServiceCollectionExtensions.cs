using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlazorForm;

/// <summary>DI registration for BlazorForm's Blazor rendering services.</summary>
public static class BlazorFormServiceCollectionExtensions
{
    /// <summary>
    /// Registers BlazorForm services, including the field renderer registry. Call
    /// <paramref name="configure"/> to override built-in renderers or register custom ones.
    /// </summary>
    public static IServiceCollection AddBlazorForm(
        this IServiceCollection services,
        Action<IBlazorFormFieldRendererRegistry>? configure = null)
    {
        var registry = new BlazorFormFieldRendererRegistry();
        configure?.Invoke(registry);
        services.TryAddSingleton<IBlazorFormFieldRendererRegistry>(registry);
        return services;
    }
}
