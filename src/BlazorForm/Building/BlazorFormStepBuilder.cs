namespace BlazorForm;

/// <summary>Fluent configuration for a wizard step.</summary>
public sealed class BlazorFormStepBuilder
{
    private readonly BlazorFormStep _step;
    internal BlazorFormStepBuilder(BlazorFormStep step) => _step = step;

    public BlazorFormStepBuilder Title(string title) { _step.Title = title; return this; }
    public BlazorFormStepBuilder Description(string description) { _step.Description = description; return this; }
    public BlazorFormStepBuilder Fields(params string[] fields) { foreach (var f in fields) _step.Fields.Add(f); return this; }
    public BlazorFormStepBuilder VisibleWhen(IBlazorFormCondition condition) { _step.VisibleWhen = condition; return this; }
}
