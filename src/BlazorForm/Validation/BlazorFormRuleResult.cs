namespace BlazorForm;

/// <summary>The result of running a single validation rule.</summary>
public readonly struct BlazorFormRuleResult
{
    private BlazorFormRuleResult(bool isValid, string? message, BlazorFormValidationSeverity severity)
    {
        IsValid = isValid;
        Message = message;
        Severity = severity;
    }

    public bool IsValid { get; }
    public string? Message { get; }
    public BlazorFormValidationSeverity Severity { get; }

    public static BlazorFormRuleResult Success() => new(true, null, BlazorFormValidationSeverity.Error);

    public static BlazorFormRuleResult Fail(string message, BlazorFormValidationSeverity severity = BlazorFormValidationSeverity.Error)
        => new(false, message, severity);
}
