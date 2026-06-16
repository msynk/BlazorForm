namespace BlazorForm;

/// <summary>
/// A selectable option for <see cref="BlazorFormFieldType.Select"/>, <see cref="BlazorFormFieldType.MultiSelect"/>
/// and <see cref="BlazorFormFieldType.Radio"/> fields.
/// </summary>
/// <param name="Value">The underlying value stored on the model when the option is chosen.</param>
/// <param name="Label">The human-readable label shown to the user.</param>
/// <param name="Disabled">Whether the option is selectable.</param>
/// <param name="Group">Optional group name, allowing options to be rendered under option-groups.</param>
public sealed record BlazorFormSelectOption(string Value, string Label, bool Disabled = false, string? Group = null)
{
    /// <summary>Creates an option where the value and label are identical.</summary>
    public static BlazorFormSelectOption Of(string value) => new(value, value);
}
