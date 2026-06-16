using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace BlazorForm;

/// <summary>
/// Reads <see cref="System.ComponentModel.DataAnnotations"/> attributes from a property and applies
/// them to a <see cref="BlazorFormFieldDefinition"/> (labels, requiredness, length/range/pattern validators).
/// </summary>
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
        }

        var displayName = member.GetCustomAttribute<DisplayNameAttribute>();
        if (displayName is not null) field.Label = displayName.DisplayName;

        // Required
        if (member.GetCustomAttribute<RequiredAttribute>() is { } req)
        {
            field.Required = true;
            field.Validators.Add(new BlazorFormRequiredRule(NullIfDefault(req.ErrorMessage)));
        }

        // String length
        if (member.GetCustomAttribute<StringLengthAttribute>() is { } sl)
        {
            field.MaxLength = sl.MaximumLength;
            field.Validators.Add(new BlazorFormMaxLengthRule(sl.MaximumLength, NullIfDefault(sl.ErrorMessage)));
            if (sl.MinimumLength > 0)
            {
                field.MinLength = sl.MinimumLength;
                field.Validators.Add(new BlazorFormMinLengthRule(sl.MinimumLength, NullIfDefault(sl.ErrorMessage)));
            }
        }

        if (member.GetCustomAttribute<MinLengthAttribute>() is { } ml)
        {
            field.MinLength = ml.Length;
            field.Validators.Add(new BlazorFormMinLengthRule(ml.Length, NullIfDefault(ml.ErrorMessage)));
        }

        if (member.GetCustomAttribute<MaxLengthAttribute>() is { } mxl)
        {
            field.MaxLength = mxl.Length;
            field.Validators.Add(new BlazorFormMaxLengthRule(mxl.Length, NullIfDefault(mxl.ErrorMessage)));
        }

        // Range
        if (member.GetCustomAttribute<RangeAttribute>() is { } range)
        {
            double? min = TryToDouble(range.Minimum);
            double? max = TryToDouble(range.Maximum);
            field.Min = min;
            field.Max = max;
            field.Validators.Add(new BlazorFormRangeRule(min, max, NullIfDefault(range.ErrorMessage)));
        }

        // Email / phone / url
        if (member.GetCustomAttribute<EmailAddressAttribute>() is not null)
        {
            field.Type = BlazorFormFieldType.Email;
            field.Validators.Add(new BlazorFormEmailRule());
        }
        if (member.GetCustomAttribute<PhoneAttribute>() is not null)
            field.Type = BlazorFormFieldType.Tel;
        if (member.GetCustomAttribute<UrlAttribute>() is not null)
            field.Type = BlazorFormFieldType.Url;

        // Regex
        if (member.GetCustomAttribute<RegularExpressionAttribute>() is { } rx)
        {
            field.Pattern = rx.Pattern;
            field.Validators.Add(new BlazorFormPatternRule(rx.Pattern, NullIfDefault(rx.ErrorMessage)));
        }

        // DataType hints
        if (member.GetCustomAttribute<DataTypeAttribute>() is { } dt)
        {
            field.Type = dt.DataType switch
            {
                DataType.Password => BlazorFormFieldType.Password,
                DataType.MultilineText => BlazorFormFieldType.TextArea,
                DataType.EmailAddress => BlazorFormFieldType.Email,
                DataType.Url => BlazorFormFieldType.Url,
                DataType.PhoneNumber => BlazorFormFieldType.Tel,
                DataType.Date => BlazorFormFieldType.Date,
                DataType.Time => BlazorFormFieldType.Time,
                DataType.DateTime => BlazorFormFieldType.DateTime,
                _ => field.Type
            };
        }

        // Editable(false) => read only
        if (member.GetCustomAttribute<EditableAttribute>() is { AllowEdit: false })
            field.ReadOnly = true;
    }

    private static string? NullIfDefault(string? message) => string.IsNullOrWhiteSpace(message) ? null : message;

    private static double? TryToDouble(object? value)
        => value is null ? null :
           double.TryParse(value.ToString(), System.Globalization.NumberStyles.Any,
               System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
}
