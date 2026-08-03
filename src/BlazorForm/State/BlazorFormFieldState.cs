namespace BlazorForm;

/// <summary>
/// A snapshot of one field's interaction and validation state, returned by
/// <see cref="BlazorFormState.GetFieldState"/>.
/// </summary>
/// <param name="IsTouched">Whether the user has interacted with the field.</param>
/// <param name="IsDirty">Whether the value differs from the one the form opened with.</param>
/// <param name="IsInvalid">
/// Whether the field currently carries an error-severity message. Warnings do not make a field
/// invalid — they are advice, not a refusal.
/// </param>
/// <param name="Messages">Every message recorded against the field, in the order they were produced.</param>
public readonly record struct BlazorFormFieldState(
    bool IsTouched,
    bool IsDirty,
    bool IsInvalid,
    IReadOnlyList<BlazorFormValidationMessage> Messages)
{
    /// <summary>The first error-severity message, or null when the field has none.</summary>
    public BlazorFormValidationMessage? Error
    {
        get
        {
            for (var i = 0; i < Messages.Count; i++)
                if (Messages[i].Severity == BlazorFormValidationSeverity.Error) return Messages[i];
            return null;
        }
    }
}
