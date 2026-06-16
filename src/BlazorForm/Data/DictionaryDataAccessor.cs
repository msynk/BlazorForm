namespace BlazorForm.Core.Data;

/// <summary>
/// Stores form data in a nested dictionary/list structure. This is the backing store used
/// when a form is driven purely by a JSON schema and there is no compiled C# model.
/// Nested objects are <see cref="Dictionary{TKey,TValue}"/> and arrays are <see cref="List{T}"/>.
/// </summary>
public sealed class DictionaryDataAccessor : IFormDataAccessor
{
    private readonly Dictionary<string, object?> _root;

    public DictionaryDataAccessor(IDictionary<string, object?>? initial = null)
        => _root = initial is null ? new() : new(initial);

    public object? Root => _root;

    public object? GetValue(string path)
    {
        var segments = FormPath.Parse(path);
        object? current = _root;
        foreach (var seg in segments)
        {
            if (current is null) return null;
            if (seg.IsIndex)
            {
                if (current is IList<object?> list && seg.Index >= 0 && seg.Index < list.Count)
                    current = list[seg.Index];
                else
                    return null;
            }
            else
            {
                if (current is IDictionary<string, object?> dict && dict.TryGetValue(seg.Name!, out var v))
                    current = v;
                else
                    return null;
            }
        }
        return current;
    }

    public void SetValue(string path, object? value)
    {
        var segments = FormPath.Parse(path);
        if (segments.Count == 0) return;

        object current = _root;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            var seg = segments[i];
            var next = segments[i + 1];

            if (seg.IsIndex)
            {
                var list = (IList<object?>)current;
                EnsureListSize(list, seg.Index);
                list[seg.Index] ??= CreateContainer(next);
                current = list[seg.Index]!;
            }
            else
            {
                var dict = (IDictionary<string, object?>)current;
                if (!dict.TryGetValue(seg.Name!, out var child) || child is null)
                {
                    child = CreateContainer(next);
                    dict[seg.Name!] = child;
                }
                current = child!;
            }
        }

        var last = segments[^1];
        if (last.IsIndex)
        {
            var list = (IList<object?>)current;
            EnsureListSize(list, last.Index);
            list[last.Index] = value;
        }
        else
        {
            ((IDictionary<string, object?>)current)[last.Name!] = value;
        }
    }

    public Type? GetElementType(string arrayPath) => typeof(object);

    private static object CreateContainer(PathSegment next)
        => next.IsIndex ? new List<object?>() : new Dictionary<string, object?>();

    private static void EnsureListSize(IList<object?> list, int index)
    {
        while (list.Count <= index)
            list.Add(null);
    }
}
