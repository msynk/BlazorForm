namespace BlazorForm;

/// <summary>A validation rule applied to a field.</summary>
public interface IBlazorFormValidationRule
{
    /// <summary>Validates the field. Synchronous rules can return a completed task.</summary>
    ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext context);

    /// <summary>
    /// True if the rule performs asynchronous work (e.g. remote calls). Used to decide whether
    /// to run the rule on every keystroke or defer it to blur/submit.
    /// </summary>
    bool IsAsync => false;
}
