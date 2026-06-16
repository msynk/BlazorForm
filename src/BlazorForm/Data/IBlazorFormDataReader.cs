namespace BlazorForm;

/// <summary>Read access to form data by path.</summary>
public interface IBlazorFormDataReader
{
    /// <summary>Reads the value at the given path, or null if absent.</summary>
    object? GetValue(string path);

    /// <summary>The underlying root object (a model instance or a dictionary).</summary>
    object? Root { get; }
}
