using BlazorForm.Core.Data;

namespace BlazorForm.Core.Schema;

/// <summary>How the operands of a <see cref="FieldCondition"/> are compared.</summary>
public enum ConditionOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Contains,
    NotContains,
    In,
    NotIn,
    IsEmpty,
    IsNotEmpty,
    IsTrue,
    IsFalse,
    Matches
}

/// <summary>How multiple conditions inside a <see cref="ConditionGroup"/> combine.</summary>
public enum ConditionLogic
{
    And,
    Or
}

/// <summary>
/// A boolean predicate evaluated against the current form data. Conditions drive
/// visibility, enablement and conditional validation.
/// </summary>
public interface ICondition
{
    /// <summary>Evaluates the condition against the supplied data reader.</summary>
    bool Evaluate(IFormDataReader data);

    /// <summary>
    /// The set of field paths this condition depends on, so the engine can re-evaluate
    /// only when a relevant value changes. Empty means "depends on everything".
    /// </summary>
    IEnumerable<string> Dependencies { get; }
}

/// <summary>
/// A single, serializable rule comparing the value at <see cref="FieldPath"/> against <see cref="Value"/>.
/// </summary>
public sealed class FieldCondition : ICondition
{
    public FieldCondition(string fieldPath, ConditionOperator @operator, object? value = null)
    {
        FieldPath = fieldPath;
        Operator = @operator;
        Value = value;
    }

    public string FieldPath { get; }
    public ConditionOperator Operator { get; }
    public object? Value { get; }

    public IEnumerable<string> Dependencies => [FieldPath];

    public bool Evaluate(IFormDataReader data)
    {
        var actual = data.GetValue(FieldPath);
        return ConditionEvaluator.Compare(actual, Operator, Value);
    }
}

/// <summary>
/// Combines child conditions with <see cref="ConditionLogic.And"/> or <see cref="ConditionLogic.Or"/>.
/// </summary>
public sealed class ConditionGroup : ICondition
{
    public ConditionGroup(ConditionLogic logic, params ICondition[] conditions)
    {
        Logic = logic;
        Conditions = [.. conditions];
    }

    public ConditionLogic Logic { get; }
    public IReadOnlyList<ICondition> Conditions { get; }

    public IEnumerable<string> Dependencies => Conditions.SelectMany(c => c.Dependencies);

    public bool Evaluate(IFormDataReader data)
        => Logic == ConditionLogic.And
            ? Conditions.All(c => c.Evaluate(data))
            : Conditions.Any(c => c.Evaluate(data));

    public static ConditionGroup All(params ICondition[] conditions) => new(ConditionLogic.And, conditions);
    public static ConditionGroup Any(params ICondition[] conditions) => new(ConditionLogic.Or, conditions);
}

/// <summary>
/// A condition backed by an arbitrary delegate. Powerful but not serializable to JSON;
/// use <see cref="FieldCondition"/> / <see cref="ConditionGroup"/> when round-tripping schemas.
/// </summary>
public sealed class PredicateCondition : ICondition
{
    private readonly Func<IFormDataReader, bool> _predicate;

    public PredicateCondition(Func<IFormDataReader, bool> predicate, params string[] dependencies)
    {
        _predicate = predicate;
        Dependencies = dependencies;
    }

    public IEnumerable<string> Dependencies { get; }

    public bool Evaluate(IFormDataReader data) => _predicate(data);
}
