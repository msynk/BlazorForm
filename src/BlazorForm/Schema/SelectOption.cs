namespace BlazorForm.Core.Schema;

/// <summary>
/// A selectable option for <see cref="FieldType.Select"/>, <see cref="FieldType.MultiSelect"/>
/// and <see cref="FieldType.Radio"/> fields.
/// </summary>
/// <param name="Value">The underlying value stored on the model when the option is chosen.</param>
/// <param name="Label">The human-readable label shown to the user.</param>
/// <param name="Disabled">Whether the option is selectable.</param>
/// <param name="Group">Optional group name, allowing options to be rendered under option-groups.</param>
public sealed record SelectOption(string Value, string Label, bool Disabled = false, string? Group = null)
{
    /// <summary>Creates an option where the value and label are identical.</summary>
    public static SelectOption Of(string value) => new(value, value);
}
