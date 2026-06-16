namespace BlazorForm.Core.Data;

/// <summary>Read access to form data by path.</summary>
public interface IFormDataReader
{
    /// <summary>Reads the value at the given path, or null if absent.</summary>
    object? GetValue(string path);

    /// <summary>The underlying root object (a model instance or a dictionary).</summary>
    object? Root { get; }
}

/// <summary>Read/write access to form data by path.</summary>
public interface IFormDataAccessor : IFormDataReader
{
    /// <summary>Writes a value at the given path, creating intermediate containers as needed.</summary>
    void SetValue(string path, object? value);

    /// <summary>
    /// The declared element type for an array path (used to materialise new items),
    /// or null if it cannot be determined.
    /// </summary>
    Type? GetElementType(string arrayPath);
}
