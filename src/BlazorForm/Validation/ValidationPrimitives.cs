using BlazorForm.Core.Data;

namespace BlazorForm.Core.Validation;

/// <summary>Severity of a validation message.</summary>
public enum ValidationSeverity
{
    Error,
    Warning,
    Info
}

/// <summary>A single validation message tied to a field path.</summary>
/// <param name="FieldPath">The path of the field the message belongs to.</param>
/// <param name="Message">Human-readable message.</param>
/// <param name="Severity">Severity of the message.</param>
public sealed record ValidationMessage(string FieldPath, string Message, ValidationSeverity Severity = ValidationSeverity.Error);

/// <summary>The result of running a single validation rule.</summary>
public readonly struct RuleResult
{
    private RuleResult(bool isValid, string? message, ValidationSeverity severity)
    {
        IsValid = isValid;
        Message = message;
        Severity = severity;
    }

    public bool IsValid { get; }
    public string? Message { get; }
    public ValidationSeverity Severity { get; }

    public static RuleResult Success() => new(true, null, ValidationSeverity.Error);

    public static RuleResult Fail(string message, ValidationSeverity severity = ValidationSeverity.Error)
        => new(false, message, severity);
}

/// <summary>Context passed to a validation rule.</summary>
public sealed class ValidationContext
{
    public ValidationContext(string fieldPath, object? value, IFormDataReader data, IServiceProvider? services = null)
    {
        FieldPath = fieldPath;
        Value = value;
        Data = data;
        Services = services;
    }

    /// <summary>The path of the field being validated.</summary>
    public string FieldPath { get; }

    /// <summary>The current value of the field being validated.</summary>
    public object? Value { get; }

    /// <summary>Read access to the whole form, for cross-field rules.</summary>
    public IFormDataReader Data { get; }

    /// <summary>Optional service provider for rules that need DI (e.g. async uniqueness checks).</summary>
    public IServiceProvider? Services { get; }
}

/// <summary>A validation rule applied to a field.</summary>
public interface IValidationRule
{
    /// <summary>Validates the field. Synchronous rules can return a completed task.</summary>
    ValueTask<RuleResult> ValidateAsync(ValidationContext context);

    /// <summary>
    /// True if the rule performs asynchronous work (e.g. remote calls). Used to decide whether
    /// to run the rule on every keystroke or defer it to blur/submit.
    /// </summary>
    bool IsAsync => false;
}
