namespace BlazorForm;

/// <summary>The result of running a single validation rule.</summary>
public readonly struct BlazorFormRuleResult
{
    private BlazorFormRuleResult(bool isValid, string? message, BlazorFormValidationSeverity severity, string? fieldPath)
    {
        IsValid = isValid;
        Message = message;
        Severity = severity;
        FieldPath = fieldPath;
    }

    public bool IsValid { get; }
    public string? Message { get; }
    public BlazorFormValidationSeverity Severity { get; }

    /// <summary>
    /// The path the message should be attached to. Null (the usual case) means the field being
    /// validated; a cross-field or form-level rule sets it to point the user at the field to fix.
    /// </summary>
    public string? FieldPath { get; }

    public static BlazorFormRuleResult Success() => new(true, null, BlazorFormValidationSeverity.Error, null);

    public static BlazorFormRuleResult Fail(string message, BlazorFormValidationSeverity severity = BlazorFormValidationSeverity.Error)
        => new(false, message, severity, null);

    /// <summary>Fails and attaches the message to a specific field path rather than the field being validated.</summary>
    public static BlazorFormRuleResult FailFor(string fieldPath, string message,
        BlazorFormValidationSeverity severity = BlazorFormValidationSeverity.Error)
        => new(false, message, severity, fieldPath);
}
