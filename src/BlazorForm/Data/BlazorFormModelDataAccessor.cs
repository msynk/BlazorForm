using System.Collections;
using System.Reflection;

namespace BlazorForm;

/// <summary>
/// Read/write access to a strongly-typed POCO via reflection, supporting nested objects and
/// <see cref="IList"/> collections. Used when a form is bound to a compiled C# model.
/// </summary>
public sealed class BlazorFormModelDataAccessor : IBlazorFormDataAccessor
{
    public BlazorFormModelDataAccessor(object model) => Root = model ?? throw new ArgumentNullException(nameof(model));

    public object? Root { get; }

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

    public void SetValue(string path, object? value)
    {
        var segments = BlazorFormPath.Parse(path);
        if (segments.Count == 0) return;

        object current = Root!;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            var seg = segments[i];
            var child = ReadSegment(current, seg);
            if (child is null)
            {
                child = CreateChild(current, seg);
                WriteSegment(current, seg, child);
            }
            current = child!;
        }

        WriteSegment(current, segments[^1], value);
    }

    public Type? GetElementType(string arrayPath)
    {
        var type = ResolveType(arrayPath);
        if (type is null) return null;
        if (type.IsArray) return type.GetElementType();
        var enumerable = type.GetInterfaces().Append(type)
            .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable?.GetGenericArguments()[0];
    }

    private Type? ResolveType(string path)
    {
        var segments = BlazorFormPath.Parse(path);
        Type? currentType = Root!.GetType();
        foreach (var seg in segments)
        {
            if (currentType is null) return null;
            if (seg.IsIndex)
            {
                currentType = currentType.IsArray
                    ? currentType.GetElementType()
                    : currentType.GetGenericArguments().FirstOrDefault();
            }
            else
            {
                currentType = currentType.GetProperty(seg.Name!,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)?.PropertyType;
            }
        }
        return currentType;
    }

    private static object? ReadSegment(object target, BlazorFormPathSegment seg)
    {
        if (seg.IsIndex)
        {
            if (target is IList list && seg.Index >= 0 && seg.Index < list.Count)
                return list[seg.Index];
            return null;
        }

        var prop = target.GetType().GetProperty(seg.Name!,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return prop?.GetValue(target);
    }

    private static void WriteSegment(object target, BlazorFormPathSegment seg, object? value)
    {
        if (seg.IsIndex)
        {
            if (target is IList list)
            {
                while (list.Count <= seg.Index) list.Add(null);
                list[seg.Index] = value;
            }
            return;
        }

        var prop = target.GetType().GetProperty(seg.Name!,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop is null || !prop.CanWrite) return;
        prop.SetValue(target, Convert(value, prop.PropertyType));
    }

    private static object CreateChild(object parent, BlazorFormPathSegment seg)
    {
        if (seg.IsIndex)
            return new object(); // index containers are pre-existing lists; placeholder
        var prop = parent.GetType().GetProperty(seg.Name!,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        var type = prop?.PropertyType ?? typeof(object);
        return Activator.CreateInstance(type) ?? new object();
    }

    private static object? Convert(object? value, Type targetType)
    {
        if (value is null) return null;
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (underlying.IsInstanceOfType(value)) return value;
        try
        {
            if (underlying.IsEnum && value is string es)
                return Enum.Parse(underlying, es, ignoreCase: true);
            return System.Convert.ChangeType(value, underlying, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return value;
        }
    }
}
