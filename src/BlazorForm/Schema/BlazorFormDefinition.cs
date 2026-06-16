namespace BlazorForm;

/// <summary>
/// A complete form schema: the ordered set of fields, optional wizard steps and metadata.
/// A <see cref="BlazorFormDefinition"/> is the single source of truth a renderer consumes.
/// </summary>
public sealed class BlazorFormDefinition
{
    /// <summary>Optional form title.</summary>
    public string? Title { get; set; }

    /// <summary>Optional form description.</summary>
    public string? Description { get; set; }

    /// <summary>The CLR model type this schema was generated from, when applicable.</summary>
    public Type? ModelType { get; set; }

    /// <summary>Top-level fields.</summary>
    public IList<BlazorFormFieldDefinition> Fields { get; set; } = new List<BlazorFormFieldDefinition>();

    /// <summary>Wizard steps. When non-empty, the form is multi-step.</summary>
    public IList<BlazorFormStep> Steps { get; set; } = new List<BlazorFormStep>();

    /// <summary>True when the form is configured as a multi-step wizard.</summary>
    public bool IsWizard => Steps.Count > 0;

    /// <summary>Finds a top-level field by name.</summary>
    public BlazorFormFieldDefinition? FindField(string name)
        => Fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Enumerates every field in the schema, recursing into objects and array item templates.</summary>
    public IEnumerable<BlazorFormFieldDefinition> AllFields()
    {
        foreach (var f in Fields)
            foreach (var d in Descend(f))
                yield return d;

        static IEnumerable<BlazorFormFieldDefinition> Descend(BlazorFormFieldDefinition f)
        {
            yield return f;
            foreach (var c in f.Children)
                foreach (var d in Descend(c))
                    yield return d;
            if (f.ItemTemplate is not null)
                foreach (var d in Descend(f.ItemTemplate))
                    yield return d;
        }
    }
}
