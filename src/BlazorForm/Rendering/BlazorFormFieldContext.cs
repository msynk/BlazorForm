namespace BlazorForm;

/// <summary>
/// Everything a field renderer needs: the field definition, its absolute data path, the owning
/// <see cref="BlazorFormState"/>, and helpers to read/write the value and report validation messages.
/// </summary>
public sealed class BlazorFormFieldContext
{
    public BlazorFormFieldContext(BlazorFormState state, BlazorFormFieldDefinition field, string path)
    {
        State = state;
        Field = field;
        Path = path;
    }

    public BlazorFormState State { get; }
    public BlazorFormFieldDefinition Field { get; }

    /// <summary>The absolute path of this field within the form data (e.g. <c>Items[0].Product</c>).</summary>
    public string Path { get; }

    /// <summary>A DOM-safe id derived from the path.</summary>
    public string ElementId => "ff_" + Path.Replace('.', '_').Replace('[', '_').Replace("]", "");

    public bool IsDisabled => State.IsDisabled(Field);

    public object? Value => State.GetValue(Path);

    /// <summary>The current value formatted for an HTML input.</summary>
    public string StringValue => BlazorFormValueConverter.ToInputString(Value, Field.Type);

    public bool BoolValue => Value is bool b ? b : Value is "true" or "on" or "True";

    public IReadOnlyList<BlazorFormValidationMessage> Messages => State.MessagesFor(Path);
    public bool HasError => Messages.Any(m => m.Severity == BlazorFormValidationSeverity.Error);
    public bool ShowMessages => State.IsTouched(Path) || State.SubmitCount > 0;

    /// <summary>Writes a parsed value from raw input text and revalidates the field.</summary>
    public async Task SetFromStringAsync(string? raw)
    {
        var parsed = BlazorFormValueConverter.FromInputString(raw, Field.ValueType, Field.Type);
        State.SetValue(Path, parsed);
        await State.ValidateFieldAsync(Field, Path, includeAsync: false);
    }

    /// <summary>Writes a value directly and revalidates the field.</summary>
    public async Task SetValueAsync(object? value)
    {
        State.SetValue(Path, value);
        await State.ValidateFieldAsync(Field, Path, includeAsync: false);
    }

    /// <summary>Runs async validators for this field (e.g. on blur).</summary>
    public Task ValidateAsync() => State.ValidateFieldAsync(Field, Path, includeAsync: true).AsTask();
}
