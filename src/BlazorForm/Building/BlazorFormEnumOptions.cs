using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace BlazorForm;

/// <summary>
/// Turns an enum type into select options, honouring the labelling attributes a developer has already
/// put on the members — <c>[Display(Name = …)]</c> or <c>[Description]</c> — instead of showing the
/// raw identifier. Members marked <c>[Display(AutoGenerateField = false)]</c> are omitted.
/// </summary>
public static class BlazorFormEnumOptions
{
    /// <summary>Builds the options for <paramref name="enumType"/> in declaration (or <c>[Display(Order)]</c>) order.</summary>
    public static IReadOnlyList<BlazorFormSelectOption> For(Type enumType)
    {
        var underlying = Nullable.GetUnderlyingType(enumType) ?? enumType;
        if (!underlying.IsEnum)
            throw new ArgumentException($"'{underlying}' is not an enum type.", nameof(enumType));

        var isFlags = underlying.IsDefined(typeof(FlagsAttribute), inherit: false);
        var options = new List<(int Order, BlazorFormSelectOption Option)>();
        var index = 0;

        foreach (var name in Enum.GetNames(underlying))
        {
            var member = underlying.GetField(name, BindingFlags.Public | BindingFlags.Static);
            var display = member?.GetCustomAttribute<DisplayAttribute>();

            if (display?.GetAutoGenerateField() == false) { index++; continue; }

            // A [Flags] enum's zero member ("None") is the absence of every flag, so it can never be
            // ticked in a multi-select and would only confuse the list.
            if (isFlags && member is not null && ToInt64(member.GetRawConstantValue()) == 0) { index++; continue; }

            var label = display?.GetName()
                        ?? member?.GetCustomAttribute<DescriptionAttribute>()?.Description
                        ?? BlazorFormFieldBuilder.Humanize(name);

            options.Add((display?.GetOrder() ?? index, new BlazorFormSelectOption(name, label, Group: display?.GroupName)));
            index++;
        }

        return options.OrderBy(o => o.Order).Select(o => o.Option).ToList();
    }

    /// <summary>
    /// Widens an enum's raw constant (which may be any integral type, signed or not) to a long so the
    /// zero member can be recognised.
    /// </summary>
    internal static long ToInt64(object? rawValue) => rawValue switch
    {
        null => 0,
        ulong u => unchecked((long)u),
        IConvertible c => c.ToInt64(System.Globalization.CultureInfo.InvariantCulture),
        _ => 0
    };
}
