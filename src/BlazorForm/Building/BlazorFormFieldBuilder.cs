namespace BlazorForm;

/// <summary>
/// Fluent configuration for a single <see cref="BlazorFormFieldDefinition"/>. Returned by the form builders
/// so fields can be described with chained, readable calls.
/// </summary>
/// <remarks>
/// Constraint methods go through <see cref="BlazorFormFieldDefinition.AddValidator"/>, so calling
/// <c>.Required("…")</c> on a field whose model already carries <c>[Required]</c> replaces the generated
/// rule rather than stacking a second identical message on top of it.
/// </remarks>
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

    /// <summary>Sets the HTML <c>autocomplete</c> token, e.g. <c>email</c>, <c>street-address</c>, <c>new-password</c>.</summary>
    public BlazorFormFieldBuilder Autocomplete(string value) { _field.Autocomplete = value; return this; }

    /// <summary>Sets the HTML <c>inputmode</c> token, which chooses the on-screen keyboard on mobile.</summary>
    public BlazorFormFieldBuilder InputMode(string value) { _field.InputMode = value; return this; }

    /// <summary>Focuses this field when the form first renders.</summary>
    public BlazorFormFieldBuilder Autofocus(bool value = true) { _field.Autofocus = value; return this; }

    /// <summary>How many columns the field spans in a multi-column layout.</summary>
    public BlazorFormFieldBuilder ColumnSpan(int span) { _field.ColumnSpan = span; return this; }

    /// <summary>
    /// Hides the visible label — for a control the surrounding page already names. The label is still
    /// attached to the input as its accessible name, so the field never becomes anonymous.
    /// </summary>
    public BlazorFormFieldBuilder HideLabel(bool hide = true) { _field.ShowLabel = !hide; return this; }

    /// <summary>Static text rendered inside the control before the input — a currency symbol, an <c>@</c>.</summary>
    public BlazorFormFieldBuilder Prefix(string prefix) { _field.Prefix = prefix; return this; }

    /// <summary>Static text rendered after the input — a unit such as <c>kg</c> or <c>%</c>.</summary>
    public BlazorFormFieldBuilder Suffix(string suffix) { _field.Suffix = suffix; return this; }

    /// <summary>Shows a live "42 / 200" counter beneath a length-limited field.</summary>
    public BlazorFormFieldBuilder CharacterCount(bool show = true) { _field.ShowCharacterCount = show; return this; }

    /// <summary>
    /// Writes the value on every keystroke rather than on blur, optionally waiting
    /// <paramref name="debounceMilliseconds"/> after the last one. This is what makes a live preview,
    /// a computed total or as-you-type validation feel immediate.
    /// </summary>
    public BlazorFormFieldBuilder UpdateOnInput(int debounceMilliseconds = 0)
    {
        _field.UpdateOn = BlazorFormUpdateTrigger.Input;
        _field.DebounceMilliseconds = Math.Max(0, debounceMilliseconds);
        return this;
    }

    /// <summary>Writes the value on the browser's <c>change</c> event — for a text box, on blur.</summary>
    public BlazorFormFieldBuilder UpdateOnChange()
    {
        _field.UpdateOn = BlazorFormUpdateTrigger.Change;
        _field.DebounceMilliseconds = 0;
        return this;
    }

    /// <summary>
    /// Adds an HTML attribute to the rendered control — a <c>data-</c> hook, a <c>title</c>,
    /// <c>spellcheck="false"</c>. Attributes the renderer sets itself always win.
    /// </summary>
    public BlazorFormFieldBuilder InputAttr(string name, object? value)
    {
        _field.InputAttributes[name] = value;
        return this;
    }

    /// <summary>
    /// Offers values as <c>&lt;datalist&gt;</c> suggestions without restricting what may be typed —
    /// "the answers people usually give", as opposed to <see cref="Options(BlazorFormSelectOption[])"/>,
    /// which is a closed set.
    /// </summary>
    public BlazorFormFieldBuilder Suggest(params string[] suggestions)
    {
        foreach (var s in suggestions) _field.Suggestions.Add(s);
        return this;
    }

    // --- Type overrides ---
    public BlazorFormFieldBuilder As(BlazorFormFieldType type) { _field.Type = type; return this; }
    public BlazorFormFieldBuilder AsTextArea(int rows = 4) { _field.Type = BlazorFormFieldType.TextArea; _field.Attributes["rows"] = rows; return this; }
    public BlazorFormFieldBuilder AsEmail() => Email();
    public BlazorFormFieldBuilder AsPassword() { _field.Type = BlazorFormFieldType.Password; _field.Autocomplete ??= "current-password"; return this; }
    public BlazorFormFieldBuilder AsUrl() { _field.Type = BlazorFormFieldType.Url; return this; }
    public BlazorFormFieldBuilder AsTel() { _field.Type = BlazorFormFieldType.Tel; return this; }

    /// <summary>
    /// Renders the field as a search box. Browsers give one a clear affordance, a search key on the
    /// on-screen keyboard and history from previous searches; the value is plain text either way.
    /// </summary>
    public BlazorFormFieldBuilder AsSearch()
    {
        _field.Type = BlazorFormFieldType.Search;
        _field.InputMode ??= "search";
        return this;
    }
    public BlazorFormFieldBuilder AsColor() { _field.Type = BlazorFormFieldType.Color; return this; }
    public BlazorFormFieldBuilder AsRange() { _field.Type = BlazorFormFieldType.Range; return this; }
    public BlazorFormFieldBuilder AsRadio() { _field.Type = BlazorFormFieldType.Radio; return this; }
    public BlazorFormFieldBuilder AsMultiSelect() { _field.Type = BlazorFormFieldType.MultiSelect; return this; }
    public BlazorFormFieldBuilder AsSelect() { _field.Type = BlazorFormFieldType.Select; return this; }
    public BlazorFormFieldBuilder AsHidden() { _field.Type = BlazorFormFieldType.Hidden; return this; }
    public BlazorFormFieldBuilder AsFile() { _field.Type = BlazorFormFieldType.File; return this; }

    /// <summary>
    /// Adds a "show the characters" toggle to a password field. Opt-in, because whether a password may
    /// be revealed at all is a decision about the environment the form runs in — a shared screen, a
    /// recorded session — rather than about the field.
    /// </summary>
    public BlazorFormFieldBuilder Revealable(bool value = true)
    {
        _field.Type = BlazorFormFieldType.Password;
        _field.Attributes["reveal"] = value;
        return this;
    }

    /// <summary>
    /// Adds a one-click "empty this field" button, shown only while there is something to clear. Worth
    /// it on a touch screen, where select-all-and-delete is real work.
    /// </summary>
    public BlazorFormFieldBuilder Clearable(bool value = true)
    {
        _field.Attributes["clearable"] = value;
        return this;
    }

    /// <summary>Renders the checkbox as a switch — the same control, announced as "on/off".</summary>
    public BlazorFormFieldBuilder AsSwitch()
    {
        _field.Type = BlazorFormFieldType.Checkbox;
        _field.Attributes["switch"] = true;
        return this;
    }

    // --- Options ---
    public BlazorFormFieldBuilder Options(params BlazorFormSelectOption[] options)
    {
        foreach (var o in options) _field.Options.Add(o);
        EnsureChoiceType();
        return this;
    }

    public BlazorFormFieldBuilder Options(params (string Value, string Label)[] options)
        => Options(options.Select(o => new BlazorFormSelectOption(o.Value, o.Label)).ToArray());

    /// <summary>Adds options from an enum type's members, honouring <c>[Display]</c> / <c>[Description]</c>.</summary>
    public BlazorFormFieldBuilder OptionsFromEnum(Type enumType)
    {
        foreach (var option in BlazorFormEnumOptions.For(enumType))
            _field.Options.Add(option);
        EnsureChoiceType();
        return this;
    }

    /// <summary>Adds options from an enum type's members.</summary>
    public BlazorFormFieldBuilder OptionsFromEnum<TEnum>() where TEnum : struct, Enum => OptionsFromEnum(typeof(TEnum));

    /// <summary>
    /// Loads the options on demand — from a service, or from another field's current value to build a
    /// cascading select. List <paramref name="dependsOn"/> paths so the options reload (and the stale
    /// selection clears) whenever one of them changes.
    /// </summary>
    public BlazorFormFieldBuilder OptionsFrom(BlazorFormOptionsProvider provider, params string[] dependsOn)
    {
        _field.OptionsProvider = provider;
        foreach (var d in dependsOn) _field.OptionsDependencies.Add(d);
        EnsureChoiceType();
        return this;
    }

    /// <summary>
    /// Derives the field's value from the rest of the form — a line total, a full name, a price after
    /// discount. List <paramref name="dependsOn"/> paths so the value refreshes when they change;
    /// leave it empty to recompute on any change. Combine with <see cref="ReadOnly"/> when the user
    /// should not be able to override the result.
    /// </summary>
    public BlazorFormFieldBuilder Computed(BlazorFormComputedValue compute, params string[] dependsOn)
    {
        _field.Computed = compute;
        foreach (var d in dependsOn) _field.ComputedDependencies.Add(d);
        return this;
    }

    /// <summary>
    /// Maps a sequence of items to options, for the common "load a list and project it" case.
    /// </summary>
    /// <remarks>
    /// <typeparamref name="TItem"/> is inferred from <paramref name="load"/>, which means the loader
    /// must be typed as <c>IEnumerable&lt;TItem&gt;</c>; a source returning a concrete
    /// <c>List&lt;T&gt;</c> needs an explicit type argument (<c>OptionsFrom&lt;City&gt;(…)</c>), or use
    /// the <see cref="OptionsFrom(BlazorFormOptionsProvider, string[])" /> overload and project yourself.
    /// </remarks>
    public BlazorFormFieldBuilder OptionsFrom<TItem>(
        Func<BlazorFormOptionsContext, ValueTask<IEnumerable<TItem>>> load,
        Func<TItem, string> value,
        Func<TItem, string> label,
        params string[] dependsOn)
        => OptionsFrom(async ctx =>
        {
            var items = await load(ctx);
            return items.Select(i => new BlazorFormSelectOption(value(i), label(i))).ToList();
        }, dependsOn);

    // --- Constraints ---
    public BlazorFormFieldBuilder Required(string? message = null)
    {
        _field.Required = true;
        _field.AddValidator(new BlazorFormRequiredRule(message));
        return this;
    }

    /// <summary>
    /// Makes the field required only while <paramref name="condition"/> holds — the declarative form of
    /// "required when country is US".
    /// </summary>
    public BlazorFormFieldBuilder RequiredWhen(IBlazorFormCondition condition)
    {
        _field.RequiredWhen = condition;
        return this;
    }

    /// <inheritdoc cref="RequiredWhen(IBlazorFormCondition)" />
    public BlazorFormFieldBuilder RequiredWhen(string field, BlazorFormConditionOperator op, object? value = null)
        => RequiredWhen(new BlazorFormFieldCondition(field, op, value));

    public BlazorFormFieldBuilder MinLength(int min, string? message = null)
    {
        _field.MinLength = min;
        _field.AddValidator(new BlazorFormMinLengthRule(min, message));
        return this;
    }

    public BlazorFormFieldBuilder MaxLength(int max, string? message = null)
    {
        _field.MaxLength = max;
        _field.AddValidator(new BlazorFormMaxLengthRule(max, message));
        return this;
    }

    /// <summary>Sets both length bounds in one call.</summary>
    public BlazorFormFieldBuilder Length(int min, int max, string? message = null)
        => MinLength(min, message).MaxLength(max, message);

    public BlazorFormFieldBuilder Range(double? min, double? max, string? message = null)
    {
        _field.Min = min;
        _field.Max = max;
        _field.AddValidator(new BlazorFormRangeRule(min, max, message));
        return this;
    }

    public BlazorFormFieldBuilder Step(double step) { _field.NumericStep = step; return this; }

    /// <summary>Requires the value to be an exact multiple of <paramref name="factor"/> (JSON Schema's <c>multipleOf</c>).</summary>
    public BlazorFormFieldBuilder MultipleOf(double factor, string? message = null)
    {
        _field.NumericStep ??= factor;
        _field.AddValidator(new BlazorFormMultipleOfRule(factor, message));
        return this;
    }

    public BlazorFormFieldBuilder Pattern(string pattern, string? message = null)
    {
        _field.Pattern = pattern;
        _field.AddValidator(new BlazorFormPatternRule(pattern, message));
        return this;
    }

    public BlazorFormFieldBuilder Email(string? message = null)
    {
        _field.Type = BlazorFormFieldType.Email;
        _field.Autocomplete ??= "email";
        _field.AddValidator(new BlazorFormEmailRule(message));
        return this;
    }

    /// <summary>Marks the field as a URL and validates that it parses as an absolute one.</summary>
    public BlazorFormFieldBuilder Url(string? message = null)
    {
        _field.Type = BlazorFormFieldType.Url;
        _field.AddValidator(new BlazorFormUrlRule(message));
        return this;
    }

    /// <summary>Requires this field to equal the value at another path — the "confirm password" rule.</summary>
    public BlazorFormFieldBuilder MatchesField(string otherPath, string? otherLabel = null, string? message = null)
    {
        _field.AddValidator(new BlazorFormCompareRule(otherPath, otherLabel, message));
        return this;
    }

    public BlazorFormFieldBuilder Accept(string accept, bool multiple = false)
    {
        _field.Type = BlazorFormFieldType.File;
        _field.Accept = accept;
        _field.Multiple = multiple;
        _field.AddValidator(new BlazorFormFileRule(_field.MaxFileSize, accept));
        return this;
    }

    /// <summary>Limits each selected file's size, in bytes.</summary>
    public BlazorFormFieldBuilder MaxFileSize(long bytes, string? message = null)
    {
        _field.Type = BlazorFormFieldType.File;
        _field.MaxFileSize = bytes;
        _field.AddValidator(new BlazorFormFileRule(bytes, _field.Accept, message));
        return this;
    }

    public BlazorFormFieldBuilder Items(int? min = null, int? max = null, string? message = null)
    {
        _field.MinItems = min;
        _field.MaxItems = max;
        _field.AddValidator(new BlazorFormCollectionSizeRule(min, max, message));
        return this;
    }

    /// <summary>
    /// Creates the minimum number of rows up front, so a repeater whose schema says "at least one line"
    /// opens with a line rather than with an error about not having one.
    /// </summary>
    /// <remarks>
    /// Opt-in rather than implied by <see cref="Items"/>: on an edit form loaded from storage, "this
    /// record has no lines" can be the truth, and inventing one would be the library editing the user's
    /// data on their behalf.
    /// </remarks>
    public BlazorFormFieldBuilder SeedMinItems(bool value = true)
    {
        _field.Attributes["seedMinItems"] = value;
        return this;
    }

    /// <summary>Requires every item in an array field to be distinct.</summary>
    public BlazorFormFieldBuilder UniqueItems(string? message = null)
    {
        _field.AddValidator(new BlazorFormUniqueItemsRule(message));
        return this;
    }

    // --- Custom validation ---
    public BlazorFormFieldBuilder Validate(IBlazorFormValidationRule rule) { _field.AddValidator(rule); return this; }

    /// <summary>
    /// Restricts the rules added by <paramref name="configure"/> to the times
    /// <paramref name="condition"/> holds — FluentValidation's <c>.When(…)</c>, for a field that is
    /// visible either way but only constrained sometimes.
    /// </summary>
    public BlazorFormFieldBuilder When(IBlazorFormCondition condition, Action<BlazorFormFieldBuilder> configure)
        => When(ctx => condition.Evaluate(ctx.Data), configure);

    /// <inheritdoc cref="When(IBlazorFormCondition, Action{BlazorFormFieldBuilder})" />
    public BlazorFormFieldBuilder When(string field, BlazorFormConditionOperator op, object? value, Action<BlazorFormFieldBuilder> configure)
        => When(new BlazorFormFieldCondition(field, op, value), configure);

    /// <inheritdoc cref="When(IBlazorFormCondition, Action{BlazorFormFieldBuilder})" />
    public BlazorFormFieldBuilder When(Func<BlazorFormValidationContext, bool> condition, Action<BlazorFormFieldBuilder> configure)
    {
        // The rules are collected on a scratch field, then re-added wrapped, so the caller writes the
        // same fluent chain inside `When` as outside it.
        var scratch = new BlazorFormFieldDefinition(_field.Name, _field.Type) { ValueType = _field.ValueType };
        configure(new BlazorFormFieldBuilder(scratch));

        foreach (var rule in scratch.Validators)
            _field.Validators.Add(new BlazorFormConditionalRule(rule, condition));

        return this;
    }

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

    public BlazorFormFieldBuilder DisabledWhen(Func<IBlazorFormDataReader, bool> predicate, params string[] dependencies)
        => DisabledWhen(new BlazorFormPredicateCondition(predicate, dependencies));

    /// <summary>
    /// Clears the value as soon as the field becomes hidden, so an abandoned branch of the form does
    /// not submit values the user can no longer see.
    /// </summary>
    public BlazorFormFieldBuilder ClearOnHide(bool value = true) { _field.ClearOnHide = value; return this; }

    private void EnsureChoiceType()
    {
        if (!_field.IsChoice) _field.Type = BlazorFormFieldType.Select;
    }

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
