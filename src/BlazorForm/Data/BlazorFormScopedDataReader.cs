namespace BlazorForm;

/// <summary>
/// A view over form data that resolves a path relative to an owning object before falling back to the
/// root. This is what lets a condition written on an array item's template — <c>VisibleWhen("Kind", …)</c>
/// — mean *this row's* Kind, exactly as a computed value's dependencies already do, while a condition
/// naming a top-level field keeps working from anywhere in the form.
/// </summary>
public sealed class BlazorFormScopedDataReader : IBlazorFormDataReader
{
    private readonly IBlazorFormDataReader _inner;
    private readonly string _scope;

    private BlazorFormScopedDataReader(IBlazorFormDataReader inner, string scope)
    {
        _inner = inner;
        _scope = scope;
    }

    /// <summary>
    /// Wraps <paramref name="data"/> so paths resolve against <paramref name="scope"/> first. Returns
    /// <paramref name="data"/> unchanged for the root scope, so the common case allocates nothing.
    /// </summary>
    public static IBlazorFormDataReader For(IBlazorFormDataReader data, string scope)
        => string.IsNullOrEmpty(scope) ? data : new BlazorFormScopedDataReader(Unwrap(data), scope);

    /// <summary>Wraps <paramref name="data"/> scoped to the container that owns <paramref name="fieldPath"/>.</summary>
    public static IBlazorFormDataReader ForOwnerOf(IBlazorFormDataReader data, string fieldPath)
        => For(data, BlazorFormPath.Parent(fieldPath));

    /// <summary>The root of the underlying store — a scope never changes what "the whole form" means.</summary>
    public object? Root => _inner.Root;

    /// <summary>The object the scope points at, for rules that want the owning row rather than the model.</summary>
    public object? Scope => _inner.GetValue(_scope);

    /// <summary>The path this reader is scoped to.</summary>
    public string ScopePath => _scope;

    public object? GetValue(string path) => TryGetValue(path, out var value) ? value : null;

    /// <summary>
    /// Resolves against the scope first, then the root.
    /// </summary>
    /// <remarks>
    /// The test is whether the scoped path <em>exists</em>, not whether it holds something. A row's own
    /// field wins even when it is empty — which is the whole point of a condition such as
    /// <c>VisibleWhen("Email", IsEmpty)</c>. Falling back on "the scoped read came out null" let a
    /// root-level field of the same name answer for the row, so a blank row silently reported its
    /// neighbour's value. The fallback still catches the case it was written for: a path the row does
    /// not have at all, which is how an absolute reference from inside a repeater keeps working.
    /// </remarks>
    public bool TryGetValue(string path, out object? value)
    {
        // An empty path means "the whole store", which no scope changes.
        if (!string.IsNullOrEmpty(path)
            && _inner.TryGetValue(BlazorFormPath.Combine(_scope, path), out value))
            return true;

        return _inner.TryGetValue(path, out value);
    }

    /// <summary>Peels off an existing scope so scopes never nest into an unreadable chain.</summary>
    private static IBlazorFormDataReader Unwrap(IBlazorFormDataReader data)
        => data is BlazorFormScopedDataReader scoped ? scoped._inner : data;
}
