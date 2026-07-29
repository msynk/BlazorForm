using System.Collections;

namespace BlazorForm;

/// <summary>
/// Stores form data in a nested dictionary/list structure. This is the backing store used
/// when a form is driven purely by a JSON schema and there is no compiled C# model.
/// Nested objects are <see cref="Dictionary{TKey,TValue}"/> and arrays are <see cref="List{T}"/>.
/// </summary>
public sealed class BlazorFormDictionaryDataAccessor : IBlazorFormDataAccessor
{
    private readonly Dictionary<string, object?> _root;

    public BlazorFormDictionaryDataAccessor(IDictionary<string, object?>? initial = null)
        => _root = initial is null ? new() : new(initial);

    public object? Root => _root;

    /// <summary>
    /// True when the most recent <see cref="SetValue"/> could not be applied because an existing value
    /// on the path was not the container the path required (for example writing <c>a.b</c> when
    /// <c>a</c> already holds a string).
    /// </summary>
    public bool LastWriteFailed { get; private set; }

    public object? GetValue(string path)
    {
        var segments = BlazorFormPath.Parse(path);
        object? current = _root;
        foreach (var seg in segments)
        {
            if (current is null) return null;
            if (seg.IsIndex)
            {
                if (current is IList list && seg.Index >= 0 && seg.Index < list.Count)
                    current = list[seg.Index];
                else
                    return null;
            }
            else
            {
                if (current is IDictionary<string, object?> dict && dict.TryGetValue(seg.Name!, out var v))
                    current = v;
                else if (current is IDictionary untyped && untyped.Contains(seg.Name!))
                    current = untyped[seg.Name!];
                else
                    return null;
            }
        }
        return current;
    }

    public void SetValue(string path, object? value)
    {
        LastWriteFailed = false;

        var segments = BlazorFormPath.Parse(path);
        if (segments.Count == 0) return;

        object current = _root;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            var seg = segments[i];
            var next = segments[i + 1];

            if (seg.IsIndex)
            {
                // The path says "index into this", but the value here is not a list — writing anyway
                // would throw, so the write is abandoned and reported instead.
                if (current is not IList list) { LastWriteFailed = true; return; }
                EnsureListSize(list, seg.Index);
                if (list[seg.Index] is null || !IsContainerFor(list[seg.Index], next))
                    list[seg.Index] = CreateContainer(next);
                current = list[seg.Index]!;
            }
            else
            {
                if (current is not IDictionary<string, object?> dict) { LastWriteFailed = true; return; }
                if (!dict.TryGetValue(seg.Name!, out var child) || child is null || !IsContainerFor(child, next))
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
            if (current is not IList list) { LastWriteFailed = true; return; }
            EnsureListSize(list, last.Index);
            list[last.Index] = value;
        }
        else
        {
            if (current is not IDictionary<string, object?> dict) { LastWriteFailed = true; return; }
            dict[last.Name!] = value;
        }
    }

    /// <summary>
    /// Element types are unknown in a schema-only form, so items are created as loosely-typed
    /// dictionaries and values are stored as they arrive.
    /// </summary>
    public Type? GetElementType(string arrayPath) => typeof(object);

    private static object CreateContainer(BlazorFormPathSegment next)
        => next.IsIndex ? new List<object?>() : new Dictionary<string, object?>();

    /// <summary>Whether an existing value is already the right kind of container for the next segment.</summary>
    private static bool IsContainerFor(object? existing, BlazorFormPathSegment next)
        => next.IsIndex ? existing is IList : existing is IDictionary<string, object?>;

    private static void EnsureListSize(IList list, int index)
    {
        while (list.Count <= index)
            list.Add(null);
    }
}
