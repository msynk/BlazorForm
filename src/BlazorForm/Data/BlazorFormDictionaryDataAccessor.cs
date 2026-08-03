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

    /// <summary>
    /// Keys are matched without regard to case, because every other way a path is resolved already is:
    /// a schema lookup (<see cref="BlazorFormDefinition.FindByPath"/>), a property on a typed model,
    /// and the state's own touched/dirty/message tracking. A dictionary-backed form was the one place
    /// where <c>Email</c> and <c>email</c> were different fields — which is not a distinction the
    /// library can honour anywhere else, and <see cref="BlazorFormDefinition.Validate"/> already
    /// reports two siblings whose names differ only in case as binding to the same path.
    /// </summary>
    private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

    public BlazorFormDictionaryDataAccessor(IDictionary<string, object?>? initial = null)
    {
        _root = new Dictionary<string, object?>(KeyComparer);
        if (initial is null) return;

        // Copied one at a time rather than through the copy constructor: a caller's dictionary may hold
        // keys that differ only in case, and that constructor throws on the collision. Last one wins,
        // which is what the store would have ended up with anyway.
        foreach (var (key, value) in initial) _root[key] = value;
    }

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

    /// <summary>
    /// Reads a path, reporting whether the key is present at all — as distinct from present and null,
    /// which is a value the store can legitimately hold.
    /// </summary>
    public bool TryGetValue(string path, out object? value)
    {
        value = null;
        object? current = _root;

        foreach (var seg in BlazorFormPath.Parse(path))
        {
            if (seg.IsIndex)
            {
                if (current is not IList list || seg.Index < 0 || seg.Index >= list.Count) return false;
                current = list[seg.Index];
            }
            else if (current is IDictionary<string, object?> dict)
            {
                if (!dict.TryGetValue(seg.Name!, out current)) return false;
            }
            else if (current is IDictionary untyped)
            {
                if (!untyped.Contains(seg.Name!)) return false;
                current = untyped[seg.Name!];
            }
            else
            {
                return false;
            }
        }

        value = current;
        return true;
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
        => next.IsIndex ? new List<object?>() : NewObject();

    /// <summary>
    /// A nested object in the store. Public so a caller creating an array item by hand — or the form
    /// state creating one for a repeater — gets a container that resolves keys the same way the root
    /// does, rather than a case-sensitive one nested inside a case-insensitive store.
    /// </summary>
    public static Dictionary<string, object?> NewObject() => new(KeyComparer);

    /// <summary>Whether an existing value is already the right kind of container for the next segment.</summary>
    private static bool IsContainerFor(object? existing, BlazorFormPathSegment next)
        => next.IsIndex ? existing is IList : existing is IDictionary<string, object?>;

    private static void EnsureListSize(IList list, int index)
    {
        while (list.Count <= index)
            list.Add(null);
    }
}
