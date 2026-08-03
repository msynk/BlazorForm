using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace BlazorForm;

/// <summary>
/// Read/write access to a strongly-typed POCO via reflection, supporting nested objects and
/// <see cref="IList"/> collections. Used when a form is bound to a compiled C# model.
/// </summary>
/// <remarks>
/// Writes never throw: a value that cannot be converted to the target property type is discarded and
/// reported through <see cref="LastWriteFailed"/> instead. The UI keeps the raw text the user typed
/// (so it can be corrected) while the model keeps its last valid value.
/// </remarks>
public sealed class BlazorFormModelDataAccessor : IBlazorFormDataAccessor
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;

    // Property lookup sits on the hot path of every keystroke; reflection results are cached per type.
    private static readonly ConcurrentDictionary<(Type Type, string Name), PropertyInfo?> PropertyCache = new();

    public BlazorFormModelDataAccessor(object model) => Root = model ?? throw new ArgumentNullException(nameof(model));

    public object? Root { get; }

    /// <summary>
    /// True when the most recent <see cref="SetValue"/> could not be applied because the value did not
    /// convert to the property's type (e.g. "abc" into an <c>int</c>).
    /// </summary>
    public bool LastWriteFailed { get; private set; }

    public object? GetValue(string path)
    {
        var segments = BlazorFormPath.Parse(path);
        object? current = Root;
        foreach (var seg in segments)
        {
            if (current is null) return null;
            current = ReadSegment(current, seg);
        }
        return current;
    }

    /// <summary>
    /// Reads a path, reporting whether it exists on the model at all. A property that is null and a
    /// property that does not exist both read as null through <see cref="GetValue"/>; only this can
    /// tell them apart, which is what lets a scoped read know whether a row really owns the field.
    /// </summary>
    public bool TryGetValue(string path, out object? value)
    {
        value = null;
        var segments = BlazorFormPath.Parse(path);
        if (segments.Count == 0)
        {
            value = Root;
            return true;
        }

        object? current = Root;
        foreach (var seg in segments)
        {
            // The path continues past something that is not there, so it cannot be resolved.
            if (current is null) return false;

            if (seg.IsIndex)
            {
                if (current is not IList list || seg.Index < 0 || seg.Index >= list.Count) return false;
                current = list[seg.Index];
                continue;
            }

            var prop = FindProperty(current.GetType(), seg.Name!);
            if (prop is null || !prop.CanRead) return false;
            try { current = prop.GetValue(current); }
            catch (TargetInvocationException) { return false; }
        }

        value = current;
        return true;
    }

    public void SetValue(string path, object? value)
    {
        LastWriteFailed = false;
        var segments = BlazorFormPath.Parse(path);
        if (segments.Count == 0) return;

        object current = Root!;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            var seg = segments[i];
            var child = ReadSegment(current, seg);
            if (child is null)
            {
                var created = CreateChild(current, seg);
                if (created is null)
                {
                    LastWriteFailed = true;
                    return;
                }
                WriteSegment(current, seg, created);
                // Re-read: the write may have converted the placeholder or been rejected outright.
                child = ReadSegment(current, seg);
                if (child is null)
                {
                    LastWriteFailed = true;
                    return;
                }
            }
            current = child;
        }

        WriteSegment(current, segments[^1], value);
    }

    public Type? GetElementType(string arrayPath)
    {
        var type = ResolveType(arrayPath);
        return type is null ? null : BlazorFormFieldTypeResolver.GetEnumerableElementType(type);
    }

    /// <summary>Resolves the declared CLR type at a path without reading any values.</summary>
    public Type? ResolveType(string path)
    {
        var segments = BlazorFormPath.Parse(path);
        Type? currentType = Root!.GetType();
        foreach (var seg in segments)
        {
            if (currentType is null) return null;
            currentType = seg.IsIndex
                ? BlazorFormFieldTypeResolver.GetEnumerableElementType(currentType)
                : FindProperty(currentType, seg.Name!)?.PropertyType;
        }
        return currentType;
    }

    private static PropertyInfo? FindProperty(Type type, string name)
        => PropertyCache.GetOrAdd((type, name), static key =>
        {
            var (declaring, propertyName) = key;
            // GetProperty(..., IgnoreCase) throws AmbiguousMatchException when a derived type shadows a
            // property with `new`, so the lookup is done by hand: an exact match wins, then a
            // case-insensitive one.
            var candidates = declaring.GetProperties(Flags)
                .Where(p => p.GetIndexParameters().Length == 0)
                .ToArray();

            return candidates.FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.Ordinal))
                ?? candidates.FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));
        });

    private static object? ReadSegment(object target, BlazorFormPathSegment seg)
    {
        if (seg.IsIndex)
            return target is IList list && seg.Index >= 0 && seg.Index < list.Count ? list[seg.Index] : null;

        var prop = FindProperty(target.GetType(), seg.Name!);
        if (prop is null || !prop.CanRead) return null;
        try { return prop.GetValue(target); }
        catch (TargetInvocationException) { return null; }
    }

    private void WriteSegment(object target, BlazorFormPathSegment seg, object? value)
    {
        if (seg.IsIndex)
        {
            if (target is not IList list) { LastWriteFailed = true; return; }

            var elementType = BlazorFormFieldTypeResolver.GetEnumerableElementType(target.GetType()) ?? typeof(object);
            if (!BlazorFormValueConverter.TryCoerce(value, elementType, out var converted))
            {
                LastWriteFailed = true;
                return;
            }

            try
            {
                while (list.Count <= seg.Index) list.Add(DefaultFor(elementType));
                list[seg.Index] = converted;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or InvalidCastException)
            {
                LastWriteFailed = true;
            }
            return;
        }

        var prop = FindProperty(target.GetType(), seg.Name!);
        if (prop is null || !prop.CanWrite) { LastWriteFailed = true; return; }

        if (!BlazorFormValueConverter.TryCoerce(value, prop.PropertyType, out var result))
        {
            LastWriteFailed = true;
            return;
        }

        try
        {
            prop.SetValue(target, result);
        }
        catch (Exception ex) when (ex is ArgumentException or TargetInvocationException)
        {
            LastWriteFailed = true;
        }
    }

    /// <summary>Materialises the container needed to keep writing through <paramref name="seg"/>.</summary>
    private static object? CreateChild(object parent, BlazorFormPathSegment seg)
    {
        if (seg.IsIndex)
        {
            // The parent is the list itself, so create an element of its declared type — adding a bare
            // `new object()` to a List&lt;LineItem&gt; would throw.
            var elementType = BlazorFormFieldTypeResolver.GetEnumerableElementType(parent.GetType());
            return elementType is null ? null : Instantiate(elementType);
        }

        var prop = FindProperty(parent.GetType(), seg.Name!);
        return prop is null ? null : Instantiate(Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
    }

    private static object? Instantiate(Type type)
    {
        if (type == typeof(string)) return string.Empty;

        if (type.IsInterface || type.IsAbstract)
        {
            // Commonly the property is declared as IList<T>/ICollection<T>/IEnumerable<T>.
            var element = BlazorFormFieldTypeResolver.GetEnumerableElementType(type);
            if (element is not null)
            {
                var listType = typeof(List<>).MakeGenericType(element);
                if (type.IsAssignableFrom(listType)) return Activator.CreateInstance(listType);
            }
            return null;
        }

        try { return Activator.CreateInstance(type); }
        catch (Exception ex) when (ex is MissingMethodException or MemberAccessException or TargetInvocationException)
        {
            return null;
        }
    }

    private static object? DefaultFor(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;
}
