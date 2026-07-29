namespace BlazorForm;

/// <summary>
/// Derives a field's value from the rest of the form. This is the "calculated value" every form
/// builder ends up needing — a line total, a full name, a price after discount — expressed once in
/// the schema instead of being wired up by hand in each page that renders it.
/// </summary>
public delegate object? BlazorFormComputedValue(BlazorFormComputedContext context);

/// <summary>
/// What a <see cref="BlazorFormComputedValue"/> is given when it runs.
/// </summary>
/// <remarks>
/// The distinction that matters is <see cref="Scope"/> versus <see cref="IBlazorFormDataReader.Root"/>:
/// a computed field inside an array item is evaluated against *that item*, so the same expression
/// works whether it sits on the model or on a repeater row.
/// </remarks>
public sealed class BlazorFormComputedContext(IBlazorFormDataReader data, string fieldPath, string scopePath)
{
    /// <summary>Read access to the whole form.</summary>
    public IBlazorFormDataReader Data { get; } = data;

    /// <summary>The absolute path of the field being computed, e.g. <c>Lines[2].LineTotal</c>.</summary>
    public string FieldPath { get; } = fieldPath;

    /// <summary>The path of the object that owns the field — <c>Lines[2]</c>, or empty at the root.</summary>
    public string ScopePath { get; } = scopePath;

    /// <summary>
    /// The object the field belongs to: the array item for a field inside a repeater, the nested
    /// object for a field inside a group, otherwise the form's root model.
    /// </summary>
    public object? Scope => ScopePath.Length == 0 ? Data.Root : Data.GetValue(ScopePath);

    /// <summary>Reads a sibling value by its name relative to <see cref="ScopePath"/>.</summary>
    public object? Sibling(string relativePath)
        => Data.GetValue(BlazorFormPath.Combine(ScopePath, relativePath));

    /// <summary>Reads an absolute path from the root of the form.</summary>
    public object? Value(string absolutePath) => Data.GetValue(absolutePath);
}
