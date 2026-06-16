using System.Collections;

namespace BlazorForm;

/// <summary>Infers a sensible default <see cref="BlazorFormFieldType"/> from a CLR type.</summary>
public static class BlazorFormFieldTypeResolver
{
    public static BlazorFormFieldType Resolve(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        if (t == typeof(bool)) return BlazorFormFieldType.Checkbox;
        if (t == typeof(string)) return BlazorFormFieldType.Text;
        if (t.IsEnum) return BlazorFormFieldType.Select;

        if (t == typeof(byte) || t == typeof(sbyte) || t == typeof(short) || t == typeof(ushort) ||
            t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(ulong))
            return BlazorFormFieldType.Integer;

        if (t == typeof(float) || t == typeof(double) || t == typeof(decimal))
            return BlazorFormFieldType.Number;

        if (t == typeof(DateTime) || t == typeof(DateTimeOffset)) return BlazorFormFieldType.DateTime;
        if (t == typeof(DateOnly)) return BlazorFormFieldType.Date;
        if (t == typeof(TimeOnly) || t == typeof(TimeSpan)) return BlazorFormFieldType.Time;

        if (t != typeof(string) && typeof(IEnumerable).IsAssignableFrom(t))
            return BlazorFormFieldType.Array;

        if (t.IsClass) return BlazorFormFieldType.Object;

        return BlazorFormFieldType.Text;
    }

    /// <summary>Gets the element type of an enumerable/array type, or null.</summary>
    public static Type? GetEnumerableElementType(Type type)
    {
        if (type.IsArray) return type.GetElementType();
        var iface = type.GetInterfaces().Append(type)
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return iface?.GetGenericArguments()[0];
    }
}
