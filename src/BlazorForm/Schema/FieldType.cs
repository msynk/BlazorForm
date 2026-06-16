namespace BlazorForm.Core.Schema;

/// <summary>
/// The logical kind of a field. A renderer maps each <see cref="FieldType"/> to a concrete UI control.
/// The type is intentionally UI-agnostic: it describes intent, not a specific widget.
/// </summary>
public enum FieldType
{
    /// <summary>Single-line free text.</summary>
    Text,

    /// <summary>Multi-line free text.</summary>
    TextArea,

    /// <summary>Email address (text with email semantics/validation).</summary>
    Email,

    /// <summary>Masked secret input.</summary>
    Password,

    /// <summary>URL input.</summary>
    Url,

    /// <summary>Telephone number input.</summary>
    Tel,

    /// <summary>Whole number input.</summary>
    Integer,

    /// <summary>Decimal/floating point number input.</summary>
    Number,

    /// <summary>Boolean toggle (checkbox/switch).</summary>
    Checkbox,

    /// <summary>Single choice from a fixed list (dropdown).</summary>
    Select,

    /// <summary>Multiple choice from a fixed list.</summary>
    MultiSelect,

    /// <summary>Single choice rendered as a radio group.</summary>
    Radio,

    /// <summary>Date only.</summary>
    Date,

    /// <summary>Date and time.</summary>
    DateTime,

    /// <summary>Time only.</summary>
    Time,

    /// <summary>Color picker.</summary>
    Color,

    /// <summary>Slider/range input.</summary>
    Range,

    /// <summary>File upload.</summary>
    File,

    /// <summary>Hidden value, not displayed.</summary>
    Hidden,

    /// <summary>A nested object: a group of child fields bound to a sub-object.</summary>
    Object,

    /// <summary>A repeating list of items (array/repeater) described by an item schema.</summary>
    Array,

    /// <summary>A field rendered by a custom renderer resolved via <see cref="FieldDefinition.CustomRenderer"/>.</summary>
    Custom
}
