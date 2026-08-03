using System.Collections;

namespace BlazorForm;

/// <summary>Infers a sensible default <see cref="BlazorFormFieldType"/> from a CLR type.</summary>
public static class BlazorFormFieldTypeResolver
{
    public static BlazorFormFieldType Resolve(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        if (t == typeof(bool)) return BlazorFormFieldType.Checkbox;
        if (t == typeof(string) || t == typeof(char)) return BlazorFormFieldType.Text;

        // [Flags] enums hold a set of values, so they belong in a multi-select rather than a dropdown.
        if (t.IsEnum)
            return t.IsDefined(typeof(FlagsAttribute), inherit: false)
                ? BlazorFormFieldType.MultiSelect
                : BlazorFormFieldType.Select;

        if (t == typeof(byte) || t == typeof(sbyte) || t == typeof(short) || t == typeof(ushort) ||
            t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(ulong))
            return BlazorFormFieldType.Integer;

        if (t == typeof(float) || t == typeof(double) || t == typeof(decimal))
            return BlazorFormFieldType.Number;

        if (t == typeof(DateTime) || t == typeof(DateTimeOffset)) return BlazorFormFieldType.DateTime;
        if (t == typeof(DateOnly)) return BlazorFormFieldType.Date;
        if (t == typeof(TimeOnly) || t == typeof(TimeSpan)) return BlazorFormFieldType.Time;

        // Types that are technically classes/collections but that users always mean as a single value.
        if (t == typeof(Guid)) return BlazorFormFieldType.Text;
        if (t == typeof(Uri)) return BlazorFormFieldType.Url;
        if (t == typeof(byte[])) return BlazorFormFieldType.File;

        // A browser file is an upload, not a group of properties to edit. Without this it falls through
        // to the bottom of the method and generates a text box, which is unusable and silently so.
        if (IsBrowserFile(t)) return BlazorFormFieldType.File;
        if (typeof(Stream).IsAssignableFrom(t)) return BlazorFormFieldType.File;

        // A collection of files is one multi-file upload, not a repeater of file fields.
        if (GetEnumerableElementType(t) is { } element && (IsBrowserFile(element) || element == typeof(byte[])))
            return BlazorFormFieldType.File;

        // A collection of enum members is a closed set of choices, so it is one multi-select rather
        // than a repeater. The repeater is right for a list of things the user writes; it is absurd
        // for a list drawn from three fixed answers — "add a row, open a dropdown, pick Monday; add a
        // row, open a dropdown, pick Tuesday" instead of ticking two boxes. A [Flags] enum has always
        // rendered this way; a List<TDay> holds exactly the same information.
        if (GetEnumElementType(t) is not null) return BlazorFormFieldType.MultiSelect;

        if (typeof(IEnumerable).IsAssignableFrom(t)) return BlazorFormFieldType.Array;

        if (t.IsClass || (t.IsValueType && !t.IsPrimitive)) return BlazorFormFieldType.Object;

        return BlazorFormFieldType.Text;
    }

    /// <summary>Whether a type is, or implements, Blazor's <c>IBrowserFile</c>.</summary>
    private static bool IsBrowserFile(Type type)
        => typeof(Microsoft.AspNetCore.Components.Forms.IBrowserFile).IsAssignableFrom(type);

    /// <summary>
    /// The enum type a collection holds, or null when <paramref name="type"/> is not a collection of
    /// enum members. <c>List&lt;DayOfWeek?&gt;</c> counts: the nullability is about the element, not
    /// about what the choices are.
    /// </summary>
    public static Type? GetEnumElementType(Type type)
    {
        if (GetEnumerableElementType(type) is not { } element) return null;
        var underlying = Nullable.GetUnderlyingType(element) ?? element;
        return underlying.IsEnum ? underlying : null;
    }

    /// <summary>Gets the element type of an enumerable/array type, or null when the type is not a collection.</summary>
    public static Type? GetEnumerableElementType(Type type)
    {
        // string is IEnumerable<char>, but treating it as a collection of characters is never useful here.
        if (type == typeof(string)) return null;
        if (type.IsArray) return type.GetElementType();

        var iface = type.GetInterfaces().Append(type)
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return iface?.GetGenericArguments()[0];
    }
}
