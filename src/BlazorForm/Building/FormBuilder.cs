using System.Linq.Expressions;
using System.Reflection;
using BlazorForm.Core.Generation;
using BlazorForm.Core.Schema;

namespace BlazorForm.Core.Building;

/// <summary>Fluent configuration for a wizard step.</summary>
public sealed class StepBuilder
{
    private readonly FormStep _step;
    internal StepBuilder(FormStep step) => _step = step;

    public StepBuilder Title(string title) { _step.Title = title; return this; }
    public StepBuilder Description(string description) { _step.Description = description; return this; }
    public StepBuilder Fields(params string[] fields) { foreach (var f in fields) _step.Fields.Add(f); return this; }
    public StepBuilder VisibleWhen(ICondition condition) { _step.VisibleWhen = condition; return this; }
}

/// <summary>
/// Untyped, schema-first form builder. Use this when you have no compiled model (e.g. when
/// assembling a form by hand or augmenting a JSON-imported schema).
/// </summary>
public class FormBuilder
{
    protected readonly FormDefinition Form = new();

    public static FormBuilder Create() => new();

    /// <summary>Starts a strongly-typed builder bound to <typeparamref name="TModel"/>.</summary>
    public static FormBuilder<TModel> For<TModel>() where TModel : class => new();

    public FormBuilder Title(string title) { Form.Title = title; return this; }
    public FormBuilder Description(string description) { Form.Description = description; return this; }

    /// <summary>Adds a field of the given type and configures it.</summary>
    public FormBuilder Field(string name, FieldType type, Action<FieldBuilder>? configure = null)
    {
        var def = new FieldDefinition(name, type) { Label = FieldBuilder.Humanize(name) };
        configure?.Invoke(new FieldBuilder(def));
        Form.Fields.Add(def);
        return this;
    }

    public FormBuilder Text(string name, Action<FieldBuilder>? configure = null) => Field(name, FieldType.Text, configure);
    public FormBuilder Number(string name, Action<FieldBuilder>? configure = null) => Field(name, FieldType.Number, configure);
    public FormBuilder Checkbox(string name, Action<FieldBuilder>? configure = null) => Field(name, FieldType.Checkbox, configure);
    public FormBuilder Select(string name, Action<FieldBuilder>? configure = null) => Field(name, FieldType.Select, configure);

    /// <summary>Adds an object (nested group) field.</summary>
    public FormBuilder Object(string name, Action<FormBuilder> children, Action<FieldBuilder>? configure = null)
    {
        var def = new FieldDefinition(name, FieldType.Object) { Label = FieldBuilder.Humanize(name) };
        var childBuilder = new FormBuilder();
        children(childBuilder);
        foreach (var c in childBuilder.Form.Fields) def.Children.Add(c);
        configure?.Invoke(new FieldBuilder(def));
        Form.Fields.Add(def);
        return this;
    }

    /// <summary>Adds a repeating array field whose items are described by <paramref name="item"/>.</summary>
    public FormBuilder Array(string name, Action<FormBuilder> item, Action<FieldBuilder>? configure = null)
    {
        var def = new FieldDefinition(name, FieldType.Array) { Label = FieldBuilder.Humanize(name) };
        var itemBuilder = new FormBuilder();
        item(itemBuilder);
        var template = new FieldDefinition("item", FieldType.Object);
        foreach (var c in itemBuilder.Form.Fields) template.Children.Add(c);
        def.ItemTemplate = template;
        configure?.Invoke(new FieldBuilder(def));
        Form.Fields.Add(def);
        return this;
    }

    /// <summary>Defines a wizard step.</summary>
    public FormBuilder Step(string id, Action<StepBuilder> configure)
    {
        var step = new FormStep(id);
        configure(new StepBuilder(step));
        Form.Steps.Add(step);
        return this;
    }

    public FormDefinition Build() => Form;
}

/// <summary>
/// Strongly-typed form builder. Fields are selected with lambda expressions so names and value
/// types are inferred and refactor-safe, in the spirit of React Hook Form + Zod.
/// </summary>
public sealed class FormBuilder<TModel> : FormBuilder where TModel : class
{
    public FormBuilder() => Form.ModelType = typeof(TModel);

    public new FormBuilder<TModel> Title(string title) { Form.Title = title; return this; }
    public new FormBuilder<TModel> Description(string description) { Form.Description = description; return this; }

    /// <summary>Adds a field selected by expression, inferring its name and type from the model.</summary>
    public FormBuilder<TModel> Field<TValue>(
        Expression<Func<TModel, TValue>> selector,
        Action<FieldBuilder>? configure = null)
    {
        var member = GetMember(selector);
        var valueType = typeof(TValue);
        var def = new FieldDefinition(member.Name, FieldTypeResolver.Resolve(valueType))
        {
            ValueType = valueType,
            Label = FieldBuilder.Humanize(member.Name)
        };

        var underlying = Nullable.GetUnderlyingType(valueType) ?? valueType;
        var builder = new FieldBuilder(def);
        if (underlying.IsEnum)
            builder.OptionsFromEnum(underlying);

        ApplyDataAnnotations(member, builder);
        configure?.Invoke(builder);
        Form.Fields.Add(def);
        return this;
    }

    /// <summary>Adds a wizard step.</summary>
    public new FormBuilder<TModel> Step(string id, Action<StepBuilder> configure)
    {
        base.Step(id, configure);
        return this;
    }

    private static MemberInfo GetMember<TValue>(Expression<Func<TModel, TValue>> selector)
    {
        var body = selector.Body;
        if (body is UnaryExpression unary) body = unary.Operand;
        if (body is MemberExpression member) return member.Member;
        throw new ArgumentException("Field selector must be a simple property access, e.g. x => x.Name.", nameof(selector));
    }

    private static void ApplyDataAnnotations(MemberInfo member, FieldBuilder builder)
    {
        DataAnnotationsScanner.Apply(member, builder.Definition);
    }
}
