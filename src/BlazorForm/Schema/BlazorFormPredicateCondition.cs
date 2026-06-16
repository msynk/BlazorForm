namespace BlazorForm;

/// <summary>
/// A condition backed by an arbitrary delegate. Powerful but not serializable to JSON;
/// use <see cref="BlazorFormFieldCondition"/> / <see cref="BlazorFormConditionGroup"/> when round-tripping schemas.
/// </summary>
public sealed class BlazorFormPredicateCondition : IBlazorFormCondition
{
    private readonly Func<IBlazorFormDataReader, bool> _predicate;

    public BlazorFormPredicateCondition(Func<IBlazorFormDataReader, bool> predicate, params string[] dependencies)
    {
        _predicate = predicate;
        Dependencies = dependencies;
    }

    public IEnumerable<string> Dependencies { get; }

    public bool Evaluate(IBlazorFormDataReader data) => _predicate(data);
}
