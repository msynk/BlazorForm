using System.Collections;
using BlazorForm.Core.Schema;

namespace BlazorForm.Core.Building;

/// <summary>Infers a sensible default <see cref="FieldType"/> from a CLR type.</summary>
public static class FieldTypeResolver
{
    public static FieldType Resolve(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        if (t == typeof(bool)) return FieldType.Checkbox;
        if (t == typeof(string)) return FieldType.Text;
        if (t.IsEnum) return FieldType.Select;

        if (t == typeof(byte) || t == typeof(sbyte) || t == typeof(short) || t == typeof(ushort) ||
            t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(ulong))
            return FieldType.Integer;

        if (t == typeof(float) || t == typeof(double) || t == typeof(decimal))
            return FieldType.Number;

        if (t == typeof(DateTime) || t == typeof(DateTimeOffset)) return FieldType.DateTime;
        if (t == typeof(DateOnly)) return FieldType.Date;
        if (t == typeof(TimeOnly) || t == typeof(TimeSpan)) return FieldType.Time;

        if (t != typeof(string) && typeof(IEnumerable).IsAssignableFrom(t))
            return FieldType.Array;

        if (t.IsClass) return FieldType.Object;

        return FieldType.Text;
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
