namespace BlazorForm;

/// <summary>Read/write access to form data by path.</summary>
public interface IBlazorFormDataAccessor : IBlazorFormDataReader
{
    /// <summary>Writes a value at the given path, creating intermediate containers as needed.</summary>
    void SetValue(string path, object? value);

    /// <summary>
    /// The declared element type for an array path (used to materialise new items),
    /// or null if it cannot be determined.
    /// </summary>
    Type? GetElementType(string arrayPath);
}
