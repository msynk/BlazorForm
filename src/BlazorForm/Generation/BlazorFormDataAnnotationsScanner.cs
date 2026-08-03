using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;

namespace BlazorForm;

/// <summary>
/// Reads <see cref="System.ComponentModel.DataAnnotations"/> attributes from a property and applies
/// them to a <see cref="BlazorFormFieldDefinition"/> (labels, requiredness, length/range/pattern validators).
/// </summary>
/// <remarks>
/// Rules are added through <see cref="BlazorFormFieldDefinition.AddValidator"/>, so a field that is
/// later refined with the fluent builder ends up with one rule per constraint, not two.
/// </remarks>
public static class BlazorFormDataAnnotationsScanner
{
    public static void Apply(MemberInfo member, BlazorFormFieldDefinition field)
    {
        // Label / prompt / description
        var display = member.GetCustomAttribute<DisplayAttribute>();
        if (display is not null)
        {
            if (display.GetName() is { } name) field.Label = name;
            if (display.GetPrompt() is { } prompt) field.Placeholder = prompt;
            if (display.GetDescription() is { } desc) field.HelpText = desc;
            if (display.GetOrder() is { } order) field.Order = order;
            if (display.GetGroupName() is { } group) field.Group = group;
        }

        if (member.GetCustomAttribute<DisplayNameAttribute>() is { } displayName)
            field.Label = displayName.DisplayName;

        if (member.GetCustomAttribute<DescriptionAttribute>() is { } description && field.HelpText is null)
            field.HelpText = description.Description;

        // Required
        if (member.GetCustomAttribute<RequiredAttribute>() is { } req)
        {
            field.Required = true;
            // [Required(AllowEmptyStrings = true)] only rejects null, so an empty box is acceptable.
            field.AddValidator(req.AllowEmptyStrings
                ? new BlazorFormDelegateRule(ctx => ctx.Value is null
                    ? BlazorFormRuleResult.Fail(NullIfDefault(req.ErrorMessage) ?? ctx.Message(BlazorFormMessageKeys.Required))
                    : BlazorFormRuleResult.Success())
                : new BlazorFormRequiredRule(NullIfDefault(req.ErrorMessage)));
        }

        // String length
        if (member.GetCustomAttribute<StringLengthAttribute>() is { } sl)
        {
            field.MaxLength = sl.MaximumLength;
            field.AddValidator(new BlazorFormMaxLengthRule(sl.MaximumLength, NullIfDefault(sl.ErrorMessage)));
            if (sl.MinimumLength > 0)
            {
                field.MinLength = sl.MinimumLength;
                field.AddValidator(new BlazorFormMinLengthRule(sl.MinimumLength, NullIfDefault(sl.ErrorMessage)));
            }
        }

        // [MinLength]/[MaxLength] mean item counts on a collection, exactly as [Length] does. Mapping
        // them to the string rule on an array field made them silently do nothing: the rule reads
        // ctx.Value as a string, a List<T> is not one, and "at least one line" passed on an empty list.
        if (member.GetCustomAttribute<MinLengthAttribute>() is { } ml)
        {
            if (field.Type == BlazorFormFieldType.Array)
            {
                field.MinItems = ml.Length;
                field.AddValidator(new BlazorFormCollectionSizeRule(ml.Length, field.MaxItems, NullIfDefault(ml.ErrorMessage)));
            }
            else
            {
                field.MinLength = ml.Length;
                field.AddValidator(new BlazorFormMinLengthRule(ml.Length, NullIfDefault(ml.ErrorMessage)));
            }
        }

        if (member.GetCustomAttribute<MaxLengthAttribute>() is { } mxl)
        {
            if (field.Type == BlazorFormFieldType.Array)
            {
                field.MaxItems = mxl.Length;
                field.AddValidator(new BlazorFormCollectionSizeRule(field.MinItems, mxl.Length, NullIfDefault(mxl.ErrorMessage)));
            }
            else
            {
                field.MaxLength = mxl.Length;
                field.AddValidator(new BlazorFormMaxLengthRule(mxl.Length, NullIfDefault(mxl.ErrorMessage)));
            }
        }

        // [Length(min, max)] covers both bounds, and applies to collections as well as strings.
        if (member.GetCustomAttribute<LengthAttribute>() is { } len)
        {
            if (field.Type == BlazorFormFieldType.Array)
            {
                field.MinItems = len.MinimumLength;
                field.MaxItems = len.MaximumLength;
                field.AddValidator(new BlazorFormCollectionSizeRule(len.MinimumLength, len.MaximumLength, NullIfDefault(len.ErrorMessage)));
            }
            else
            {
                field.MinLength = len.MinimumLength;
                field.MaxLength = len.MaximumLength;
                field.AddValidator(new BlazorFormMinLengthRule(len.MinimumLength, NullIfDefault(len.ErrorMessage)));
                field.AddValidator(new BlazorFormMaxLengthRule(len.MaximumLength, NullIfDefault(len.ErrorMessage)));
            }
        }

        // Range
        if (member.GetCustomAttribute<RangeAttribute>() is { } range)
        {
            double? min = TryToDouble(range.Minimum);
            double? max = TryToDouble(range.Maximum);
            field.Min = min;
            field.Max = max;
            field.AddValidator(new BlazorFormRangeRule(min, max, NullIfDefault(range.ErrorMessage)));
        }

        // Email / phone / url
        if (member.GetCustomAttribute<EmailAddressAttribute>() is { } email)
        {
            field.Type = BlazorFormFieldType.Email;
            field.Autocomplete ??= "email";
            field.AddValidator(new BlazorFormEmailRule(NullIfDefault(email.ErrorMessage)));
        }
        if (member.GetCustomAttribute<PhoneAttribute>() is not null)
        {
            field.Type = BlazorFormFieldType.Tel;
            field.Autocomplete ??= "tel";
        }
        if (member.GetCustomAttribute<UrlAttribute>() is { } url)
        {
            field.Type = BlazorFormFieldType.Url;
            field.AddValidator(new BlazorFormUrlRule(NullIfDefault(url.ErrorMessage)));
        }

        // Regex
        if (member.GetCustomAttribute<RegularExpressionAttribute>() is { } rx)
        {
            field.Pattern = rx.Pattern;
            field.AddValidator(new BlazorFormPatternRule(rx.Pattern, NullIfDefault(rx.ErrorMessage)));
        }

        // [Compare] names a sibling property, which is exactly a form path at the same level.
        if (member.GetCustomAttribute<CompareAttribute>() is { } compare)
            field.AddValidator(new BlazorFormCompareRule(compare.OtherProperty,
                compare.OtherPropertyDisplayName ?? BlazorFormFieldBuilder.Humanize(compare.OtherProperty),
                NullIfDefault(compare.ErrorMessage)));

        // DataType hints
        if (member.GetCustomAttribute<DataTypeAttribute>() is { } dt)
        {
            field.Type = dt.DataType switch
            {
                DataType.Password => BlazorFormFieldType.Password,
                DataType.MultilineText => BlazorFormFieldType.TextArea,
                DataType.EmailAddress => BlazorFormFieldType.Email,
                DataType.Url or DataType.ImageUrl => BlazorFormFieldType.Url,
                DataType.PhoneNumber => BlazorFormFieldType.Tel,
                DataType.Date => BlazorFormFieldType.Date,
                DataType.Time => BlazorFormFieldType.Time,
                DataType.DateTime => BlazorFormFieldType.DateTime,
                DataType.Upload => BlazorFormFieldType.File,
                _ => field.Type
            };

            if (dt.DataType == DataType.Password) field.Autocomplete ??= "current-password";
        }

        // Uploads
        if (member.GetCustomAttribute<FileExtensionsAttribute>() is { } fx && !string.IsNullOrWhiteSpace(fx.Extensions))
        {
            field.Type = BlazorFormFieldType.File;
            field.Accept = string.Join(",", fx.Extensions
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(e => e.StartsWith('.') ? e : "." + e));
            field.AddValidator(new BlazorFormFileRule(field.MaxFileSize, field.Accept, NullIfDefault(fx.ErrorMessage)));
        }

        // Editable(false) => read only
        if (member.GetCustomAttribute<EditableAttribute>() is { AllowEdit: false })
            field.ReadOnly = true;
    }

    private static string? NullIfDefault(string? message) => string.IsNullOrWhiteSpace(message) ? null : message;

    private static double? TryToDouble(object? value)
    {
        if (value is null) return null;
        // [Range(typeof(DateTime), "2020-01-01", "2030-01-01")] carries strings; dates are stored as
        // OLE automation numbers so the same double-based rule and input attributes can handle them.
        if (value is DateTime dt) return dt.ToOADate();
        if (BlazorFormNumber.TryToDouble(value, out var d)) return d;
        return DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.ToOADate()
            : null;
    }
}
