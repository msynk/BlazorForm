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

    /// <summary>
    /// Replaces the English text of the built-in validation messages. Register your own
    /// <typeparamref name="TProvider"/> — typically one wrapping <c>IStringLocalizer</c> — to localise
    /// the whole library without rewriting any rules.
    /// </summary>
    public static IServiceCollection AddBlazorFormMessages<TProvider>(this IServiceCollection services)
        where TProvider : class, IBlazorFormMessageProvider
    {
        services.TryAddScoped<IBlazorFormMessageProvider, TProvider>();
        return services;
    }

    /// <summary>Registers a message provider instance (useful for a simple dictionary of overrides).</summary>
    public static IServiceCollection AddBlazorFormMessages(this IServiceCollection services, IBlazorFormMessageProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        services.TryAddSingleton(provider);
        return services;
    }
}
