using System.Text;

namespace BlazorForm;

/// <summary>
/// Everything a field renderer needs: the field definition, its absolute data path, the owning
/// <see cref="BlazorFormState"/>, and helpers to read/write the value and report validation messages.
/// </summary>
public sealed class BlazorFormFieldContext
{
    public BlazorFormFieldContext(BlazorFormState state, BlazorFormFieldDefinition field, string path)
    {
        State = state;
        Field = field;
        Path = path;
        ElementId = MakeElementId(path);
    }

    public BlazorFormState State { get; }
    public BlazorFormFieldDefinition Field { get; }

    /// <summary>The absolute path of this field within the form data (e.g. <c>Items[0].Product</c>).</summary>
    public string Path { get; }

    /// <summary>A DOM-safe id derived from the path.</summary>
    public string ElementId { get; }

    /// <summary>Id of the field's label, for controls that need <c>aria-labelledby</c> (radio and checkbox groups).</summary>
    public string LabelId => ElementId + "_label";

    /// <summary>Id of the help text element, referenced by <see cref="DescribedBy"/>.</summary>
    public string HelpId => ElementId + "_help";

    /// <summary>Id of the validation message list, referenced by <see cref="DescribedBy"/>.</summary>
    public string ErrorId => ElementId + "_error";

    /// <summary>Whether the field is disabled by a condition.</summary>
    public bool IsDisabled => State.IsDisabled(Field, Path);

    /// <summary>Whether the field is read-only.</summary>
    public bool IsReadOnly => State.IsReadOnly(Field);

    /// <summary>Whether the field is required right now (including <see cref="BlazorFormFieldDefinition.RequiredWhen"/>).</summary>
    public bool IsRequired => State.IsRequired(Field, Path);

    /// <summary>
    /// The form data, scoped to the object that owns this field: reading <c>"Quantity"</c> from a field
    /// inside a repeater row gives that row's quantity. Absolute paths still resolve.
    /// </summary>
    public IBlazorFormDataReader Data => BlazorFormScopedDataReader.ForOwnerOf(State.Data, Path);

    /// <summary>The choices to render, resolved through the field's options provider when it has one.</summary>
    public IReadOnlyList<BlazorFormSelectOption> Options => State.OptionsFor(Field, Path);

    /// <summary>True while an asynchronous options provider is loading this field's choices.</summary>
    public bool IsLoadingOptions => State.IsLoadingOptions(Path);

    public object? Value => State.GetValue(Path);

    /// <summary>
    /// The schema's extra HTML attributes, ready to splat. Null when there are none, so a renderer's
    /// <c>@attributes</c> emits nothing at all rather than an empty dictionary's worth of churn.
    /// </summary>
    /// <remarks>
    /// Built once and reused. It is read on every render of every field, and handing Blazor a fresh
    /// dictionary each time would make an unchanged splat look like a change to diff.
    /// </remarks>
    public IReadOnlyDictionary<string, object>? InputAttributes => _inputAttributes ??= BuildInputAttributes();

    private IReadOnlyDictionary<string, object>? _inputAttributes;
    private bool _inputAttributesBuilt;

    private IReadOnlyDictionary<string, object>? BuildInputAttributes()
    {
        if (_inputAttributesBuilt) return null;
        _inputAttributesBuilt = true;

        if (Field.InputAttributes.Count == 0) return null;

        var map = new Dictionary<string, object>(Field.InputAttributes.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in Field.InputAttributes)
            if (value is not null) map[key] = value;
        return map.Count == 0 ? null : map;
    }

    /// <summary>Resolves a piece of the library's own UI text through the registered message provider.</summary>
    public string Text(string key, params object?[] args) => State.Text(key, args);

    /// <summary>The current value formatted for an HTML input.</summary>
    public string StringValue => BlazorFormValueConverter.ToInputString(Value, Field.Type);

    public bool BoolValue => Value switch
    {
        bool b => b,
        string s => s is "true" or "True" or "on" or "1",
        _ => false
    };

    public IReadOnlyList<BlazorFormValidationMessage> Messages => State.MessagesFor(Path);
    public bool HasError => Messages.Any(m => m.Severity == BlazorFormValidationSeverity.Error);

    /// <summary>
    /// Whether validation messages should be shown yet. Errors stay hidden until the user has
    /// interacted with the field or tried to submit, so a blank form does not open covered in red.
    /// </summary>
    public bool ShowMessages => State.IsTouched(Path) || State.IsSubmitted;

    /// <summary>Value for the <c>aria-invalid</c> attribute, or null when the attribute should be omitted.</summary>
    public string? AriaInvalid => HasError && ShowMessages ? "true" : null;

    /// <summary>
    /// The ids an input should point <c>aria-describedby</c> at: its help text and, when they are
    /// showing, its validation messages. Null when there is nothing to describe.
    /// </summary>
    public string? DescribedBy
    {
        get
        {
            var hasHelp = !string.IsNullOrEmpty(Field.HelpText);
            var hasMessages = ShowMessages && Messages.Count > 0;
            if (!hasHelp && !hasMessages) return null;

            var sb = new StringBuilder();
            if (hasHelp) sb.Append(HelpId);
            if (hasMessages)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(ErrorId);
            }
            return sb.ToString();
        }
    }

    /// <summary>Writes a parsed value from raw input text and revalidates according to the form's trigger.</summary>
    public async Task SetFromStringAsync(string? raw)
    {
        var parsed = BlazorFormValueConverter.FromInputString(raw, Field.ValueType, Field.Type);
        State.SetValue(Path, parsed);
        if (State.ShouldValidate(Path, BlazorFormValidationTrigger.OnChange))
            await State.ValidateFieldAsync(Field, Path, includeAsync: false);
    }

    /// <summary>Writes a value directly and revalidates according to the form's trigger.</summary>
    public async Task SetValueAsync(object? value)
    {
        State.SetValue(Path, value);
        if (State.ShouldValidate(Path, BlazorFormValidationTrigger.OnChange))
            await State.ValidateFieldAsync(Field, Path, includeAsync: false);
    }

    /// <summary>
    /// Handles the field losing focus: marks it touched and runs validation — including async rules,
    /// which are too expensive for every keystroke — when the configured trigger calls for it.
    /// </summary>
    public async Task BlurAsync()
    {
        State.MarkTouched(Path);
        if (State.ShouldValidate(Path, BlazorFormValidationTrigger.OnBlur))
            await State.ValidateFieldAsync(Field, Path, includeAsync: true);
    }

    /// <summary>Runs every rule for this field, including async ones, regardless of the configured trigger.</summary>
    public Task ValidateAsync() => State.ValidateFieldAsync(Field, Path, includeAsync: true).AsTask();

    /// <summary>Loads the field's options if it has a provider and they are not cached yet.</summary>
    public ValueTask EnsureOptionsAsync() => State.EnsureOptionsAsync(Field, Path);

    /// <summary>
    /// Turns a data path into an id that is valid in HTML and safe in a CSS selector:
    /// <c>Items[0].Product</c> becomes <c>ff_Items_0_Product</c>.
    /// </summary>
    private static string MakeElementId(string path)
    {
        var sb = new StringBuilder(path.Length + 3).Append("ff_");
        foreach (var c in path)
        {
            if (c == ']') continue; // the opening bracket already became the separator
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }
        return sb.ToString();
    }
}
