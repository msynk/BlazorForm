namespace BlazorForm;

/// <summary>How the operands of a <see cref="BlazorFormFieldCondition"/> are compared.</summary>
public enum BlazorFormConditionOperator
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
