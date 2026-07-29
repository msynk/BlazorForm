using System.Linq.Expressions;
using System.Reflection;

namespace BlazorForm;

/// <summary>
/// Strongly-typed form builder. Fields are selected with lambda expressions so names and value
/// types are inferred and refactor-safe, in the spirit of React Hook Form + Zod.
/// </summary>
public sealed class BlazorFormBuilder<TModel> : BlazorFormBuilder where TModel : class
{
    public BlazorFormBuilder() => Form.ModelType = typeof(TModel);

    public new BlazorFormBuilder<TModel> Title(string title) { Form.Title = title; return this; }
    public new BlazorFormBuilder<TModel> Description(string description) { Form.Description = description; return this; }
    public new BlazorFormBuilder<TModel> Columns(int columns) { base.Columns(columns); return this; }

    /// <inheritdoc cref="BlazorFormBuilder.Static" />
    public new BlazorFormBuilder<TModel> Static(string name, string? heading = null, string? text = null,
        Action<BlazorFormFieldBuilder>? configure = null)
    {
        base.Static(name, heading, text, configure);
        return this;
    }

    /// <summary>
    /// Adds a field selected by expression, inferring its name and type from the model. Nested members
    /// are supported — <c>x =&gt; x.Address.City</c> produces a field bound to the path
    /// <c>Address.City</c>, so a single flat form can reach into an object graph.
    /// </summary>
    public BlazorFormBuilder<TModel> Field<TValue>(
        Expression<Func<TModel, TValue>> selector,
        Action<BlazorFormFieldBuilder>? configure = null)
    {
        var (path, member) = ResolvePath(selector);
        var valueType = member is PropertyInfo p ? p.PropertyType : typeof(TValue);

        var def = new BlazorFormFieldDefinition(path, BlazorFormFieldTypeResolver.Resolve(valueType))
        {
            ValueType = valueType,
            Label = BlazorFormFieldBuilder.Humanize(member.Name)
        };

        var builder = new BlazorFormFieldBuilder(def);

        var underlying = Nullable.GetUnderlyingType(valueType) ?? valueType;
        if (underlying.IsEnum)
            builder.OptionsFromEnum(underlying);

        BlazorFormDataAnnotationsScanner.Apply(member, def);
        configure?.Invoke(builder);
        Add(def);
        return this;
    }

    /// <summary>
    /// Adds a nested object group whose children are configured against the child type, so their
    /// expressions and DataAnnotations work exactly as they do at the top level.
    /// </summary>
    public BlazorFormBuilder<TModel> Object<TChild>(
        Expression<Func<TModel, TChild>> selector,
        Action<BlazorFormBuilder<TChild>> children,
        Action<BlazorFormFieldBuilder>? configure = null)
        where TChild : class
    {
        var (path, member) = ResolvePath(selector);

        var def = new BlazorFormFieldDefinition(path, BlazorFormFieldType.Object)
        {
            ValueType = typeof(TChild),
            Label = BlazorFormFieldBuilder.Humanize(member.Name)
        };

        var childBuilder = new BlazorFormBuilder<TChild>();
        children(childBuilder);
        foreach (var c in childBuilder.Build().Fields) def.Children.Add(c);

        BlazorFormDataAnnotationsScanner.Apply(member, def);
        configure?.Invoke(new BlazorFormFieldBuilder(def));
        Add(def);
        return this;
    }

    /// <summary>
    /// Adds a repeating array whose item schema is configured against the element type. Use the
    /// <see cref="Field{TValue}"/> overloads inside <paramref name="item"/> to describe one element.
    /// </summary>
    public BlazorFormBuilder<TModel> Array<TItem>(
        Expression<Func<TModel, IEnumerable<TItem>?>> selector,
        Action<BlazorFormBuilder<TItem>> item,
        Action<BlazorFormFieldBuilder>? configure = null)
        where TItem : class
    {
        var (path, member) = ResolvePath(selector);

        var def = new BlazorFormFieldDefinition(path, BlazorFormFieldType.Array)
        {
            ValueType = member is PropertyInfo p ? p.PropertyType : typeof(IEnumerable<TItem>),
            Label = BlazorFormFieldBuilder.Humanize(member.Name)
        };

        var itemBuilder = new BlazorFormBuilder<TItem>();
        item(itemBuilder);
        var template = new BlazorFormFieldDefinition("item", BlazorFormFieldType.Object) { ValueType = typeof(TItem) };
        foreach (var c in itemBuilder.Build().Fields) template.Children.Add(c);
        def.ItemTemplate = template;

        BlazorFormDataAnnotationsScanner.Apply(member, def);
        configure?.Invoke(new BlazorFormFieldBuilder(def));
        Add(def);
        return this;
    }

    /// <summary>
    /// Adds a field whose value is derived from its owning object rather than typed by the user — an
    /// order total, a full name, a line total. The field is read-only by default; pass
    /// <paramref name="configure"/> to change that.
    /// </summary>
    /// <param name="selector">The field the computed value is written to.</param>
    /// <param name="compute">
    /// Receives the object the field belongs to. At the top level that is the model; inside an
    /// <see cref="Array{TItem}"/> template it is the item, so the same expression works on a repeater
    /// row without knowing its index.
    /// </param>
    /// <param name="dependsOn">
    /// Paths whose changes trigger a recompute, named relative to the owning object. Leave null to
    /// recompute on any change.
    /// </param>
    /// <param name="configure">Further configuration for the generated field.</param>
    public BlazorFormBuilder<TModel> Computed<TValue>(
        Expression<Func<TModel, TValue>> selector,
        Func<TModel, TValue> compute,
        string[]? dependsOn = null,
        Action<BlazorFormFieldBuilder>? configure = null)
        => Field(selector, f =>
        {
            f.ReadOnly()
             .Computed(ctx => ctx.Scope is TModel owner ? compute(owner) : default(TValue), dependsOn ?? []);
            configure?.Invoke(f);
        });

    /// <summary>Adds a wizard step.</summary>
    public new BlazorFormBuilder<TModel> Step(string id, Action<BlazorFormStepBuilder> configure)
    {
        base.Step(id, configure);
        return this;
    }

    /// <summary>Adds a rule validating the whole model.</summary>
    public new BlazorFormBuilder<TModel> Validate(IBlazorFormValidationRule rule)
    {
        base.Validate(rule);
        return this;
    }

    /// <summary>
    /// Adds a cross-field rule over the whole model, e.g. "end date must be after start date".
    /// <paramref name="fieldPath"/> chooses which field shows the message.
    /// </summary>
    public BlazorFormBuilder<TModel> MustAll(Func<TModel, bool> predicate, string message, string? fieldPath = null)
        => Validate(new BlazorFormDelegateRule(ctx => ctx.Data.Root is not TModel model || predicate(model)
            ? BlazorFormRuleResult.Success()
            : fieldPath is null ? BlazorFormRuleResult.Fail(message) : BlazorFormRuleResult.FailFor(fieldPath, message)));

    /// <summary>The path of a member, for use in conditions and step definitions: <c>Path(x =&gt; x.Address.City)</c>.</summary>
    public static string Path<TValue>(Expression<Func<TModel, TValue>> selector)
        => BlazorFormExpressionPath.Of(selector);

    private static (string Path, MemberInfo Member) ResolvePath<TValue>(Expression<Func<TModel, TValue>> selector)
        => BlazorFormExpressionPath.Resolve(selector);
}
