using System.Linq.Expressions;

namespace BlazorForm;

/// <summary>Fluent configuration for a wizard step.</summary>
public sealed class BlazorFormStepBuilder
{
    private readonly BlazorFormStep _step;
    internal BlazorFormStepBuilder(BlazorFormStep step) => _step = step;

    public BlazorFormStepBuilder Title(string title) { _step.Title = title; return this; }
    public BlazorFormStepBuilder Description(string description) { _step.Description = description; return this; }

    /// <summary>Adds fields to the step by path.</summary>
    public BlazorFormStepBuilder Fields(params string[] fields)
    {
        foreach (var f in fields) _step.Fields.Add(f);
        return this;
    }

    /// <summary>
    /// Adds a field to the step by expression, so renaming the property updates the step too.
    /// Nested members work: <c>Field&lt;Order&gt;(x =&gt; x.Address.City)</c>.
    /// </summary>
    public BlazorFormStepBuilder Field<TModel>(Expression<Func<TModel, object?>> selector)
    {
        _step.Fields.Add(BlazorFormExpressionPath.Of(selector));
        return this;
    }

    public BlazorFormStepBuilder VisibleWhen(IBlazorFormCondition condition) { _step.VisibleWhen = condition; return this; }

    /// <summary>Shows the step only while the value at <paramref name="field"/> satisfies the comparison.</summary>
    public BlazorFormStepBuilder VisibleWhen(string field, BlazorFormConditionOperator op, object? value = null)
        => VisibleWhen(new BlazorFormFieldCondition(field, op, value));
}
