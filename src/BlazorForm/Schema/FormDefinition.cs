namespace BlazorForm.Core.Schema;

/// <summary>
/// One step of a multi-step (wizard) form. References top-level fields by name and may carry
/// a condition controlling whether the step is shown at all.
/// </summary>
public sealed class FormStep
{
    public FormStep(string id, string? title = null)
    {
        Id = id;
        Title = title;
    }

    /// <summary>Stable identifier for the step.</summary>
    public string Id { get; set; }

    /// <summary>Title shown in the stepper.</summary>
    public string? Title { get; set; }

    /// <summary>Optional longer description.</summary>
    public string? Description { get; set; }

    /// <summary>Names of the top-level fields belonging to this step, in order.</summary>
    public IList<string> Fields { get; set; } = new List<string>();

    /// <summary>When set and evaluates false, the step is skipped.</summary>
    public ICondition? VisibleWhen { get; set; }
}

/// <summary>
/// A complete form schema: the ordered set of fields, optional wizard steps and metadata.
/// A <see cref="FormDefinition"/> is the single source of truth a renderer consumes.
/// </summary>
public sealed class FormDefinition
{
    /// <summary>Optional form title.</summary>
    public string? Title { get; set; }

    /// <summary>Optional form description.</summary>
    public string? Description { get; set; }

    /// <summary>The CLR model type this schema was generated from, when applicable.</summary>
    public Type? ModelType { get; set; }

    /// <summary>Top-level fields.</summary>
    public IList<FieldDefinition> Fields { get; set; } = new List<FieldDefinition>();

    /// <summary>Wizard steps. When non-empty, the form is multi-step.</summary>
    public IList<FormStep> Steps { get; set; } = new List<FormStep>();

    /// <summary>True when the form is configured as a multi-step wizard.</summary>
    public bool IsWizard => Steps.Count > 0;

    /// <summary>Finds a top-level field by name.</summary>
    public FieldDefinition? FindField(string name)
        => Fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Enumerates every field in the schema, recursing into objects and array item templates.</summary>
    public IEnumerable<FieldDefinition> AllFields()
    {
        foreach (var f in Fields)
            foreach (var d in Descend(f))
                yield return d;

        static IEnumerable<FieldDefinition> Descend(FieldDefinition f)
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
