using BlazorForm.Core.Validation;

namespace BlazorForm.Core.Schema;

/// <summary>
/// Describes a single field in a form: what it is, how it should be labelled and constrained,
/// when it is visible, and how it is validated. This is the central, UI-agnostic unit of a schema.
/// </summary>
public sealed class FieldDefinition
{
    public FieldDefinition(string name, FieldType type)
    {
        Name = name;
        Type = type;
    }

    /// <summary>The field key relative to its parent (not the full path). Required and unique among siblings.</summary>
    public string Name { get; set; }

    /// <summary>The logical field type.</summary>
    public FieldType Type { get; set; }

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
    public IList<SelectOption> Options { get; set; } = new List<SelectOption>();

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
    public ICondition? VisibleWhen { get; set; }

    /// <summary>When set and evaluates true, the field is disabled.</summary>
    public ICondition? DisabledWhen { get; set; }

    /// <summary>Validation rules applied to this field.</summary>
    public IList<IValidationRule> Validators { get; set; } = new List<IValidationRule>();

    // --- Composition ---
    /// <summary>Child fields for <see cref="FieldType.Object"/>.</summary>
    public IList<FieldDefinition> Children { get; set; } = new List<FieldDefinition>();

    /// <summary>
    /// Template describing each element of a <see cref="FieldType.Array"/>. For arrays of objects this
    /// is itself an <see cref="FieldType.Object"/> with <see cref="Children"/>; for arrays of scalars it
    /// is a simple field such as <see cref="FieldType.Text"/>.
    /// </summary>
    public FieldDefinition? ItemTemplate { get; set; }

    public int? MinItems { get; set; }
    public int? MaxItems { get; set; }

    /// <summary>Key resolving a custom renderer for <see cref="FieldType.Custom"/> (and overrides).</summary>
    public string? CustomRenderer { get; set; }

    /// <summary>Extra arbitrary hints/attributes for renderers (e.g. rows, css classes, icons).</summary>
    public IDictionary<string, object?> Attributes { get; set; } = new Dictionary<string, object?>();

    /// <summary>Convenience: is this a container (object or array)?</summary>
    public bool IsContainer => Type is FieldType.Object or FieldType.Array;
}
