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

    /// <summary>Adds a field selected by expression, inferring its name and type from the model.</summary>
    public BlazorFormBuilder<TModel> Field<TValue>(
        Expression<Func<TModel, TValue>> selector,
        Action<BlazorFormFieldBuilder>? configure = null)
    {
        var member = GetMember(selector);
        var valueType = typeof(TValue);
        var def = new BlazorFormFieldDefinition(member.Name, BlazorFormFieldTypeResolver.Resolve(valueType))
        {
            ValueType = valueType,
            Label = BlazorFormFieldBuilder.Humanize(member.Name)
        };

        var underlying = Nullable.GetUnderlyingType(valueType) ?? valueType;
        var builder = new BlazorFormFieldBuilder(def);
        if (underlying.IsEnum)
            builder.OptionsFromEnum(underlying);

        ApplyDataAnnotations(member, builder);
        configure?.Invoke(builder);
        Form.Fields.Add(def);
        return this;
    }

    /// <summary>Adds a wizard step.</summary>
    public new BlazorFormBuilder<TModel> Step(string id, Action<BlazorFormStepBuilder> configure)
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

    private static void ApplyDataAnnotations(MemberInfo member, BlazorFormFieldBuilder builder)
    {
        BlazorFormDataAnnotationsScanner.Apply(member, builder.Definition);
    }
}
