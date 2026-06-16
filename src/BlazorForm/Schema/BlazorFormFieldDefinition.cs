namespace BlazorForm;

/// <summary>
/// Describes a single field in a form: what it is, how it should be labelled and constrained,
/// when it is visible, and how it is validated. This is the central, UI-agnostic unit of a schema.
/// </summary>
public sealed class BlazorFormFieldDefinition
{
    public BlazorFormFieldDefinition(string name, BlazorFormFieldType type)
    {
        Name = name;
        Type = type;
    }

    /// <summary>The field key relative to its parent (not the full path). Required and unique among siblings.</summary>
    public string Name { get; set; }

    /// <summary>The logical field type.</summary>
    public BlazorFormFieldType Type { get; set; }

    /// <summary>The CLR type of the value, when known (drives parsing and array item creation).</summary>
    public Type? ValueType { get; set; }

    /// <summary>Human-readable label.</summary>
    public string? Label { get; set; }

    /// <summary>Placeholder text for empty inputs.</summary>
    public string? Placeholder { get; set; }

    /// <summary>Help/hint text shown beneath the field.</summary>
    public string? HelpText { get; set; }

    /// <summary>Whether the field must have a value. Also added as a validation rule when built.</summary>
    public bool Required { get; set; }

    /// <summary>Whether the field is read-only.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Default value applied when the form initialises and the value is missing.</summary>
    public object? DefaultValue { get; set; }

    /// <summary>Sort order within the parent/group (ascending).</summary>
    public int Order { get; set; }

    /// <summary>Optional visual group name to cluster related fields.</summary>
    public string? Group { get; set; }

    /// <summary>Options for select/radio/multiselect fields.</summary>
    public IList<BlazorFormSelectOption> Options { get; set; } = new List<BlazorFormSelectOption>();

    // --- Constraints (also surfaced as native input attributes) ---
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? NumericStep { get; set; }
    public string? Pattern { get; set; }

    // --- File fields ---
    public bool Multiple { get; set; }
    public string? Accept { get; set; }

    // --- Conditional behaviour ---
    /// <summary>When set and evaluates false, the field is hidden and excluded from validation.</summary>
    public IBlazorFormCondition? VisibleWhen { get; set; }

    /// <summary>When set and evaluates true, the field is disabled.</summary>
    public IBlazorFormCondition? DisabledWhen { get; set; }

    /// <summary>Validation rules applied to this field.</summary>
    public IList<IBlazorFormValidationRule> Validators { get; set; } = new List<IBlazorFormValidationRule>();

    // --- Composition ---
    /// <summary>Child fields for <see cref="BlazorFormFieldType.Object"/>.</summary>
    public IList<BlazorFormFieldDefinition> Children { get; set; } = new List<BlazorFormFieldDefinition>();

    /// <summary>
    /// Template describing each element of a <see cref="BlazorFormFieldType.Array"/>. For arrays of objects this
    /// is itself an <see cref="BlazorFormFieldType.Object"/> with <see cref="Children"/>; for arrays of scalars it
    /// is a simple field such as <see cref="BlazorFormFieldType.Text"/>.
    /// </summary>
    public BlazorFormFieldDefinition? ItemTemplate { get; set; }

    public int? MinItems { get; set; }
    public int? MaxItems { get; set; }

    /// <summary>Key resolving a custom renderer for <see cref="BlazorFormFieldType.Custom"/> (and overrides).</summary>
    public string? CustomRenderer { get; set; }

    /// <summary>Extra arbitrary hints/attributes for renderers (e.g. rows, css classes, icons).</summary>
    public IDictionary<string, object?> Attributes { get; set; } = new Dictionary<string, object?>();

    /// <summary>Convenience: is this a container (object or array)?</summary>
    public bool IsContainer => Type is BlazorFormFieldType.Object or BlazorFormFieldType.Array;
}
