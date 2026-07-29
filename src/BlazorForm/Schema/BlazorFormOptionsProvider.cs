namespace BlazorForm;

/// <summary>
/// Loads the choices for a select/radio/multi-select field on demand. This is what makes cascading
/// selects possible — "cities" reading the currently selected "country" — and lets options come from a
/// service instead of being baked into the schema.
/// </summary>
public delegate ValueTask<IReadOnlyList<BlazorFormSelectOption>> BlazorFormOptionsProvider(BlazorFormOptionsContext context);

/// <summary>What an <see cref="BlazorFormOptionsProvider"/> is given when it runs.</summary>
public sealed class BlazorFormOptionsContext(
    BlazorFormFieldDefinition field,
    string fieldPath,
    IBlazorFormDataReader data,
    IServiceProvider? services,
    CancellationToken cancellationToken = default)
{
    /// <summary>The field whose options are being loaded.</summary>
    public BlazorFormFieldDefinition Field { get; } = field;

    /// <summary>The absolute path of the field within the form data.</summary>
    public string FieldPath { get; } = fieldPath;

    /// <summary>Read access to the whole form, for options that depend on other values.</summary>
    public IBlazorFormDataReader Data { get; } = data;

    /// <summary>Service provider for resolving whatever the provider needs (HTTP clients, repositories…).</summary>
    public IServiceProvider? Services { get; } = services;

    /// <summary>Cancelled when a newer load supersedes this one, or the form is disposed.</summary>
    public CancellationToken CancellationToken { get; } = cancellationToken;

    /// <summary>Reads a dependency value, e.g. the selected country for a cities lookup.</summary>
    public object? Value(string path) => Data.GetValue(path);
}
