namespace BlazorForm;

/// <summary>
/// Fluent configuration for a single <see cref="BlazorFormFieldDefinition"/>. Returned by the form builders
/// so fields can be described with chained, readable calls.
/// </summary>
public sealed class BlazorFormFieldBuilder
{
    private readonly BlazorFormFieldDefinition _field;

    public BlazorFormFieldBuilder(BlazorFormFieldDefinition field) => _field = field;

    /// <summary>The field being configured.</summary>
    public BlazorFormFieldDefinition Definition => _field;

    // --- Metadata ---
    public BlazorFormFieldBuilder Label(string label) { _field.Label = label; return this; }
    public BlazorFormFieldBuilder Placeholder(string placeholder) { _field.Placeholder = placeholder; return this; }
    public BlazorFormFieldBuilder Help(string help) { _field.HelpText = help; return this; }
    public BlazorFormFieldBuilder Order(int order) { _field.Order = order; return this; }
    public BlazorFormFieldBuilder Group(string group) { _field.Group = group; return this; }
    public BlazorFormFieldBuilder Default(object? value) { _field.DefaultValue = value; return this; }
    public BlazorFormFieldBuilder ReadOnly(bool readOnly = true) { _field.ReadOnly = readOnly; return this; }
    public BlazorFormFieldBuilder Attr(string key, object? value) { _field.Attributes[key] = value; return this; }
    public BlazorFormFieldBuilder CustomRenderer(string key) { _field.CustomRenderer = key; _field.Type = BlazorFormFieldType.Custom; return this; }

    // --- Type overrides ---
    public BlazorFormFieldBuilder As(BlazorFormFieldType type) { _field.Type = type; return this; }
    public BlazorFormFieldBuilder AsTextArea(int rows = 4) { _field.Type = BlazorFormFieldType.TextArea; _field.Attributes["rows"] = rows; return this; }
    public BlazorFormFieldBuilder AsEmail() { _field.Type = BlazorFormFieldType.Email; _field.Validators.Add(new BlazorFormEmailRule()); return this; }
    public BlazorFormFieldBuilder AsPassword() { _field.Type = BlazorFormFieldType.Password; return this; }
    public BlazorFormFieldBuilder AsUrl() { _field.Type = BlazorFormFieldType.Url; return this; }
    public BlazorFormFieldBuilder AsTel() { _field.Type = BlazorFormFieldType.Tel; return this; }
    public BlazorFormFieldBuilder AsColor() { _field.Type = BlazorFormFieldType.Color; return this; }
    public BlazorFormFieldBuilder AsRange() { _field.Type = BlazorFormFieldType.Range; return this; }
    public BlazorFormFieldBuilder AsRadio() { _field.Type = BlazorFormFieldType.Radio; return this; }
    public BlazorFormFieldBuilder AsMultiSelect() { _field.Type = BlazorFormFieldType.MultiSelect; return this; }
    public BlazorFormFieldBuilder AsHidden() { _field.Type = BlazorFormFieldType.Hidden; return this; }

    // --- Options ---
    public BlazorFormFieldBuilder Options(params BlazorFormSelectOption[] options)
    {
        foreach (var o in options) _field.Options.Add(o);
        if (_field.Type is not (BlazorFormFieldType.Select or BlazorFormFieldType.MultiSelect or BlazorFormFieldType.Radio))
            _field.Type = BlazorFormFieldType.Select;
        return this;
    }

    public BlazorFormFieldBuilder Options(params (string Value, string Label)[] options)
        => Options(options.Select(o => new BlazorFormSelectOption(o.Value, o.Label)).ToArray());

    /// <summary>Adds options from an enum type's members.</summary>
    public BlazorFormFieldBuilder OptionsFromEnum(Type enumType)
    {
        foreach (var name in Enum.GetNames(enumType))
            _field.Options.Add(new BlazorFormSelectOption(name, Humanize(name)));
        if (_field.Type is not (BlazorFormFieldType.Select or BlazorFormFieldType.MultiSelect or BlazorFormFieldType.Radio))
            _field.Type = BlazorFormFieldType.Select;
        return this;
    }

    // --- Constraints ---
    public BlazorFormFieldBuilder Required(string? message = null)
    {
        _field.Required = true;
        _field.Validators.Add(new BlazorFormRequiredRule(message));
        return this;
    }

    public BlazorFormFieldBuilder MinLength(int min, string? message = null)
    {
        _field.MinLength = min;
        _field.Validators.Add(new BlazorFormMinLengthRule(min, message));
        return this;
    }

    public BlazorFormFieldBuilder MaxLength(int max, string? message = null)
    {
        _field.MaxLength = max;
        _field.Validators.Add(new BlazorFormMaxLengthRule(max, message));
        return this;
    }

    public BlazorFormFieldBuilder Range(double? min, double? max, string? message = null)
    {
        _field.Min = min;
        _field.Max = max;
        _field.Validators.Add(new BlazorFormRangeRule(min, max, message));
        return this;
    }

    public BlazorFormFieldBuilder Step(double step) { _field.NumericStep = step; return this; }

    public BlazorFormFieldBuilder Pattern(string pattern, string? message = null)
    {
        _field.Pattern = pattern;
        _field.Validators.Add(new BlazorFormPatternRule(pattern, message));
        return this;
    }

    public BlazorFormFieldBuilder Email(string? message = null)
    {
        _field.Type = BlazorFormFieldType.Email;
        _field.Validators.Add(new BlazorFormEmailRule(message));
        return this;
    }

    public BlazorFormFieldBuilder Accept(string accept, bool multiple = false)
    {
        _field.Type = BlazorFormFieldType.File;
        _field.Accept = accept;
        _field.Multiple = multiple;
        return this;
    }

    public BlazorFormFieldBuilder Items(int? min = null, int? max = null, string? message = null)
    {
        _field.MinItems = min;
        _field.MaxItems = max;
        _field.Validators.Add(new BlazorFormCollectionSizeRule(min, max, message));
        return this;
    }

    // --- Custom validation ---
    public BlazorFormFieldBuilder Validate(IBlazorFormValidationRule rule) { _field.Validators.Add(rule); return this; }

    public BlazorFormFieldBuilder Must(Func<object?, bool> predicate, string message)
    {
        _field.Validators.Add(new BlazorFormDelegateRule(ctx => predicate(ctx.Value)
            ? BlazorFormRuleResult.Success() : BlazorFormRuleResult.Fail(message)));
        return this;
    }

    public BlazorFormFieldBuilder Must(Func<BlazorFormValidationContext, bool> predicate, string message)
    {
        _field.Validators.Add(new BlazorFormDelegateRule(ctx => predicate(ctx)
            ? BlazorFormRuleResult.Success() : BlazorFormRuleResult.Fail(message)));
        return this;
    }

    public BlazorFormFieldBuilder MustAsync(Func<BlazorFormValidationContext, ValueTask<bool>> predicate, string message)
    {
        _field.Validators.Add(new BlazorFormAsyncDelegateRule(async ctx => await predicate(ctx)
            ? BlazorFormRuleResult.Success() : BlazorFormRuleResult.Fail(message)));
        return this;
    }

    // --- Conditional behaviour ---
    public BlazorFormFieldBuilder VisibleWhen(IBlazorFormCondition condition) { _field.VisibleWhen = condition; return this; }

    public BlazorFormFieldBuilder VisibleWhen(string field, BlazorFormConditionOperator op, object? value = null)
        => VisibleWhen(new BlazorFormFieldCondition(field, op, value));

    public BlazorFormFieldBuilder VisibleWhen(Func<IBlazorFormDataReader, bool> predicate, params string[] dependencies)
        => VisibleWhen(new BlazorFormPredicateCondition(predicate, dependencies));

    public BlazorFormFieldBuilder DisabledWhen(IBlazorFormCondition condition) { _field.DisabledWhen = condition; return this; }

    public BlazorFormFieldBuilder DisabledWhen(string field, BlazorFormConditionOperator op, object? value = null)
        => DisabledWhen(new BlazorFormFieldCondition(field, op, value));

    internal static string Humanize(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var sb = new System.Text.StringBuilder(name.Length + 4);
        sb.Append(char.ToUpperInvariant(name[0]));
        for (var i = 1; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
                sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }
}
