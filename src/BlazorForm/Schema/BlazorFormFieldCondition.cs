namespace BlazorForm;

/// <summary>
/// A single, serializable rule comparing the value at <see cref="FieldPath"/> against <see cref="Value"/>.
/// </summary>
public sealed class BlazorFormFieldCondition : IBlazorFormCondition
{
    public BlazorFormFieldCondition(string fieldPath, BlazorFormConditionOperator @operator, object? value = null)
    {
        FieldPath = fieldPath;
        Operator = @operator;
        Value = value;
    }

    public string FieldPath { get; }
    public BlazorFormConditionOperator Operator { get; }
    public object? Value { get; }

    public IEnumerable<string> Dependencies => [FieldPath];

    public bool Evaluate(IBlazorFormDataReader data)
    {
        var actual = data.GetValue(FieldPath);
        return BlazorFormConditionEvaluator.Compare(actual, Operator, Value);
    }
}
