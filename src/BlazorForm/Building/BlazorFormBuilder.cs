namespace BlazorForm;

/// <summary>
/// Untyped, schema-first form builder. Use this when you have no compiled model (e.g. when
/// assembling a form by hand or augmenting a JSON-imported schema).
/// </summary>
public class BlazorFormBuilder
{
    /// <summary>The definition being assembled. Derived builders add to it directly.</summary>
    protected BlazorFormDefinition Form { get; } = new();

    public static BlazorFormBuilder Create() => new();

    /// <summary>Starts a strongly-typed builder bound to <typeparamref name="TModel"/>.</summary>
    public static BlazorFormBuilder<TModel> For<TModel>() where TModel : class => new();

    public BlazorFormBuilder Title(string title) { Form.Title = title; return this; }
    public BlazorFormBuilder Description(string description) { Form.Description = description; return this; }

    /// <summary>Lays the form out in <paramref name="columns"/> responsive columns.</summary>
    public BlazorFormBuilder Columns(int columns) { Form.Columns = Math.Max(1, columns); return this; }

    /// <summary>Adds a field of the given type and configures it.</summary>
    public BlazorFormBuilder Field(string name, BlazorFormFieldType type, Action<BlazorFormFieldBuilder>? configure = null)
    {
        var def = new BlazorFormFieldDefinition(name, type) { Label = BlazorFormFieldBuilder.Humanize(name) };
        configure?.Invoke(new BlazorFormFieldBuilder(def));
        Add(def);
        return this;
    }

    public BlazorFormBuilder Text(string name, Action<BlazorFormFieldBuilder>? configure = null) => Field(name, BlazorFormFieldType.Text, configure);
    public BlazorFormBuilder TextArea(string name, Action<BlazorFormFieldBuilder>? configure = null) => Field(name, BlazorFormFieldType.TextArea, configure);
    public BlazorFormBuilder Email(string name, Action<BlazorFormFieldBuilder>? configure = null) => Field(name, BlazorFormFieldType.Email, configure);
    public BlazorFormBuilder Password(string name, Action<BlazorFormFieldBuilder>? configure = null) => Field(name, BlazorFormFieldType.Password, configure);
    public BlazorFormBuilder Number(string name, Action<BlazorFormFieldBuilder>? configure = null) => Field(name, BlazorFormFieldType.Number, configure);
    public BlazorFormBuilder Integer(string name, Action<BlazorFormFieldBuilder>? configure = null) => Field(name, BlazorFormFieldType.Integer, configure);
    public BlazorFormBuilder Checkbox(string name, Action<BlazorFormFieldBuilder>? configure = null) => Field(name, BlazorFormFieldType.Checkbox, configure);
    public BlazorFormBuilder Select(string name, Action<BlazorFormFieldBuilder>? configure = null) => Field(name, BlazorFormFieldType.Select, configure);
    public BlazorFormBuilder Radio(string name, Action<BlazorFormFieldBuilder>? configure = null) => Field(name, BlazorFormFieldType.Radio, configure);
    public BlazorFormBuilder MultiSelect(string name, Action<BlazorFormFieldBuilder>? configure = null) => Field(name, BlazorFormFieldType.MultiSelect, configure);
    public BlazorFormBuilder Date(string name, Action<BlazorFormFieldBuilder>? configure = null) => Field(name, BlazorFormFieldType.Date, configure);
    public BlazorFormBuilder File(string name, Action<BlazorFormFieldBuilder>? configure = null) => Field(name, BlazorFormFieldType.File, configure);

    /// <summary>
    /// Adds presentational content — a section heading and optional paragraph — that binds to no value
    /// and is never validated. A long form reads as a set of titled sections instead of one
    /// undifferentiated column, without the page having to interleave its own markup with the fields.
    /// </summary>
    /// <param name="name">A unique key. It names nothing in the data; it only has to be distinct.</param>
    /// <param name="heading">Section heading. Pass null for a paragraph or divider on its own.</param>
    /// <param name="text">Explanatory text below the heading.</param>
    /// <param name="configure">Further configuration — a divider, a heading level, a column span.</param>
    public BlazorFormBuilder Static(string name, string? heading = null, string? text = null,
        Action<BlazorFormFieldBuilder>? configure = null)
        => Field(name, BlazorFormFieldType.Static, f =>
        {
            if (heading is not null) f.Label(heading); else f.HideLabel();
            if (text is not null) f.Help(text);
            configure?.Invoke(f);
        });

    /// <summary>Adds an object (nested group) field.</summary>
    public BlazorFormBuilder Object(string name, Action<BlazorFormBuilder> children, Action<BlazorFormFieldBuilder>? configure = null)
    {
        Add(BuildObject(name, children, configure));
        return this;
    }

    /// <summary>Adds a repeating array field whose items are described by <paramref name="item"/>.</summary>
    public BlazorFormBuilder Array(string name, Action<BlazorFormBuilder> item, Action<BlazorFormFieldBuilder>? configure = null)
    {
        Add(BuildArray(name, item, configure));
        return this;
    }

    /// <summary>
    /// Adds an array of scalars (strings, numbers, …) rather than of objects — the shape JSON Schema
    /// writes as <c>{"type":"array","items":{"type":"string"}}</c>.
    /// </summary>
    public BlazorFormBuilder ArrayOf(string name, BlazorFormFieldType itemType, Action<BlazorFormFieldBuilder>? configure = null,
        Action<BlazorFormFieldBuilder>? configureItem = null)
    {
        Add(BuildScalarArray(name, itemType, configure, configureItem));
        return this;
    }

    /// <summary>Defines a wizard step.</summary>
    public BlazorFormBuilder Step(string id, Action<BlazorFormStepBuilder> configure)
    {
        var step = new BlazorFormStep(id);
        configure(new BlazorFormStepBuilder(step));
        Form.Steps.Add(step);
        return this;
    }

    /// <summary>
    /// Adds a rule validating the form as a whole. Use <see cref="BlazorFormRuleResult.FailFor"/> inside
    /// the rule to attach the message to the field the user should fix.
    /// </summary>
    public BlazorFormBuilder Validate(IBlazorFormValidationRule rule)
    {
        Form.Validators.Add(rule);
        return this;
    }

    /// <summary>
    /// Adds a cross-field rule over the whole form. <paramref name="fieldPath"/> chooses which field
    /// the message appears under; leave it null for a form-level message.
    /// </summary>
    public BlazorFormBuilder MustAll(Func<IBlazorFormDataReader, bool> predicate, string message, string? fieldPath = null)
        => Validate(new BlazorFormDelegateRule(ctx => predicate(ctx.Data)
            ? BlazorFormRuleResult.Success()
            : fieldPath is null ? BlazorFormRuleResult.Fail(message) : BlazorFormRuleResult.FailFor(fieldPath, message)));

    public BlazorFormDefinition Build() => Form;

    // ---------------------------------------------------------------- internals

    /// <summary>
    /// Appends a field, rejecting a duplicate name. Two siblings with the same name would resolve to
    /// the same data path and silently overwrite each other's value.
    /// </summary>
    protected void Add(BlazorFormFieldDefinition field)
    {
        if (Form.Fields.Any(f => string.Equals(f.Name, field.Name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"A field named '{field.Name}' has already been added to this form.", nameof(field));
        Form.Fields.Add(field);
    }

    protected static BlazorFormFieldDefinition BuildObject(
        string name, Action<BlazorFormBuilder> children, Action<BlazorFormFieldBuilder>? configure)
    {
        var def = new BlazorFormFieldDefinition(name, BlazorFormFieldType.Object) { Label = BlazorFormFieldBuilder.Humanize(name) };
        var childBuilder = new BlazorFormBuilder();
        children(childBuilder);
        foreach (var c in childBuilder.Form.Fields) def.Children.Add(c);
        configure?.Invoke(new BlazorFormFieldBuilder(def));
        return def;
    }

    protected static BlazorFormFieldDefinition BuildArray(
        string name, Action<BlazorFormBuilder> item, Action<BlazorFormFieldBuilder>? configure)
    {
        var def = new BlazorFormFieldDefinition(name, BlazorFormFieldType.Array) { Label = BlazorFormFieldBuilder.Humanize(name) };
        var itemBuilder = new BlazorFormBuilder();
        item(itemBuilder);
        var template = new BlazorFormFieldDefinition("item", BlazorFormFieldType.Object);
        foreach (var c in itemBuilder.Form.Fields) template.Children.Add(c);
        def.ItemTemplate = template;
        configure?.Invoke(new BlazorFormFieldBuilder(def));
        return def;
    }

    protected static BlazorFormFieldDefinition BuildScalarArray(
        string name, BlazorFormFieldType itemType,
        Action<BlazorFormFieldBuilder>? configure, Action<BlazorFormFieldBuilder>? configureItem)
    {
        var def = new BlazorFormFieldDefinition(name, BlazorFormFieldType.Array) { Label = BlazorFormFieldBuilder.Humanize(name) };
        var template = new BlazorFormFieldDefinition("item", itemType);
        configureItem?.Invoke(new BlazorFormFieldBuilder(template));
        def.ItemTemplate = template;
        configure?.Invoke(new BlazorFormFieldBuilder(def));
        return def;
    }
}
