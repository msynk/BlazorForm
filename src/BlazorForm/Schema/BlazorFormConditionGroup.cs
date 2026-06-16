namespace BlazorForm;

/// <summary>
/// Combines child conditions with <see cref="BlazorFormConditionLogic.And"/> or <see cref="BlazorFormConditionLogic.Or"/>.
/// </summary>
public sealed class BlazorFormConditionGroup : IBlazorFormCondition
{
    public BlazorFormConditionGroup(BlazorFormConditionLogic logic, params IBlazorFormCondition[] conditions)
    {
        Logic = logic;
        Conditions = [.. conditions];
    }

    public BlazorFormConditionLogic Logic { get; }
    public IReadOnlyList<IBlazorFormCondition> Conditions { get; }

    public IEnumerable<string> Dependencies => Conditions.SelectMany(c => c.Dependencies);

    public bool Evaluate(IBlazorFormDataReader data)
        => Logic == BlazorFormConditionLogic.And
            ? Conditions.All(c => c.Evaluate(data))
            : Conditions.Any(c => c.Evaluate(data));

    public static BlazorFormConditionGroup All(params IBlazorFormCondition[] conditions) => new(BlazorFormConditionLogic.And, conditions);
    public static BlazorFormConditionGroup Any(params IBlazorFormCondition[] conditions) => new(BlazorFormConditionLogic.Or, conditions);
}
