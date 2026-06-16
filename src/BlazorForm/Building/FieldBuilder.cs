using BlazorForm.Core.Data;
using BlazorForm.Core.Schema;
using BlazorForm.Core.Validation;

namespace BlazorForm.Core.Building;

/// <summary>
/// Fluent configuration for a single <see cref="FieldDefinition"/>. Returned by the form builders
/// so fields can be described with chained, readable calls.
/// </summary>
public sealed class FieldBuilder
{
    private readonly FieldDefinition _field;

    public FieldBuilder(FieldDefinition field) => _field = field;

    /// <summary>The field being configured.</summary>
    public FieldDefinition Definition => _field;

    // --- Metadata ---
    public FieldBuilder Label(string label) { _field.Label = label; return this; }
    public FieldBuilder Placeholder(string placeholder) { _field.Placeholder = placeholder; return this; }
    public FieldBuilder Help(string help) { _field.HelpText = help; return this; }
    public FieldBuilder Order(int order) { _field.Order = order; return this; }
    public FieldBuilder Group(string group) { _field.Group = group; return this; }
    public FieldBuilder Default(object? value) { _field.DefaultValue = value; return this; }
    public FieldBuilder ReadOnly(bool readOnly = true) { _field.ReadOnly = readOnly; return this; }
    public FieldBuilder Attr(string key, object? value) { _field.Attributes[key] = value; return this; }
    public FieldBuilder CustomRenderer(string key) { _field.CustomRenderer = key; _field.Type = FieldType.Custom; return this; }

    // --- Type overrides ---
    public FieldBuilder As(FieldType type) { _field.Type = type; return this; }
    public FieldBuilder AsTextArea(int rows = 4) { _field.Type = FieldType.TextArea; _field.Attributes["rows"] = rows; return this; }
    public FieldBuilder AsEmail() { _field.Type = FieldType.Email; _field.Validators.Add(new EmailRule()); return this; }
    public FieldBuilder AsPassword() { _field.Type = FieldType.Password; return this; }
    public FieldBuilder AsUrl() { _field.Type = FieldType.Url; return this; }
    public FieldBuilder AsTel() { _field.Type = FieldType.Tel; return this; }
    public FieldBuilder AsColor() { _field.Type = FieldType.Color; return this; }
    public FieldBuilder AsRange() { _field.Type = FieldType.Range; return this; }
    public FieldBuilder AsRadio() { _field.Type = FieldType.Radio; return this; }
    public FieldBuilder AsMultiSelect() { _field.Type = FieldType.MultiSelect; return this; }
    public FieldBuilder AsHidden() { _field.Type = FieldType.Hidden; return this; }

    // --- Options ---
    public FieldBuilder Options(params SelectOption[] options)
    {
        foreach (var o in options) _field.Options.Add(o);
        if (_field.Type is not (FieldType.Select or FieldType.MultiSelect or FieldType.Radio))
            _field.Type = FieldType.Select;
        return this;
    }

    public FieldBuilder Options(params (string Value, string Label)[] options)
        => Options(options.Select(o => new SelectOption(o.Value, o.Label)).ToArray());

    /// <summary>Adds options from an enum type's members.</summary>
    public FieldBuilder OptionsFromEnum(Type enumType)
    {
        foreach (var name in Enum.GetNames(enumType))
            _field.Options.Add(new SelectOption(name, Humanize(name)));
        if (_field.Type is not (FieldType.Select or FieldType.MultiSelect or FieldType.Radio))
            _field.Type = FieldType.Select;
        return this;
    }

    // --- Constraints ---
    public FieldBuilder Required(string? message = null)
    {
        _field.Required = true;
        _field.Validators.Add(new RequiredRule(message));
        return this;
    }

    public FieldBuilder MinLength(int min, string? message = null)
    {
        _field.MinLength = min;
        _field.Validators.Add(new MinLengthRule(min, message));
        return this;
    }

    public FieldBuilder MaxLength(int max, string? message = null)
    {
        _field.MaxLength = max;
        _field.Validators.Add(new MaxLengthRule(max, message));
        return this;
    }

    public FieldBuilder Range(double? min, double? max, string? message = null)
    {
        _field.Min = min;
        _field.Max = max;
        _field.Validators.Add(new RangeRule(min, max, message));
        return this;
    }

    public FieldBuilder Step(double step) { _field.NumericStep = step; return this; }

    public FieldBuilder Pattern(string pattern, string? message = null)
    {
        _field.Pattern = pattern;
        _field.Validators.Add(new PatternRule(pattern, message));
        return this;
    }

    public FieldBuilder Email(string? message = null)
    {
        _field.Type = FieldType.Email;
        _field.Validators.Add(new EmailRule(message));
        return this;
    }

    public FieldBuilder Accept(string accept, bool multiple = false)
    {
        _field.Type = FieldType.File;
        _field.Accept = accept;
        _field.Multiple = multiple;
        return this;
    }

    public FieldBuilder Items(int? min = null, int? max = null, string? message = null)
    {
        _field.MinItems = min;
        _field.MaxItems = max;
        _field.Validators.Add(new CollectionSizeRule(min, max, message));
        return this;
    }

    // --- Custom validation ---
    public FieldBuilder Validate(IValidationRule rule) { _field.Validators.Add(rule); return this; }

    public FieldBuilder Must(Func<object?, bool> predicate, string message)
    {
        _field.Validators.Add(new DelegateRule(ctx => predicate(ctx.Value)
            ? RuleResult.Success() : RuleResult.Fail(message)));
        return this;
    }

    public FieldBuilder Must(Func<ValidationContext, bool> predicate, string message)
    {
        _field.Validators.Add(new DelegateRule(ctx => predicate(ctx)
            ? RuleResult.Success() : RuleResult.Fail(message)));
        return this;
    }

    public FieldBuilder MustAsync(Func<ValidationContext, ValueTask<bool>> predicate, string message)
    {
        _field.Validators.Add(new AsyncDelegateRule(async ctx => await predicate(ctx)
            ? RuleResult.Success() : RuleResult.Fail(message)));
        return this;
    }

    // --- Conditional behaviour ---
    public FieldBuilder VisibleWhen(ICondition condition) { _field.VisibleWhen = condition; return this; }

    public FieldBuilder VisibleWhen(string field, ConditionOperator op, object? value = null)
        => VisibleWhen(new FieldCondition(field, op, value));

    public FieldBuilder VisibleWhen(Func<IFormDataReader, bool> predicate, params string[] dependencies)
        => VisibleWhen(new PredicateCondition(predicate, dependencies));

    public FieldBuilder DisabledWhen(ICondition condition) { _field.DisabledWhen = condition; return this; }

    public FieldBuilder DisabledWhen(string field, ConditionOperator op, object? value = null)
        => DisabledWhen(new FieldCondition(field, op, value));

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
