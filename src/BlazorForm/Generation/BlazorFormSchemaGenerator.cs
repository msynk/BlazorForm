using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace BlazorForm;

/// <summary>
/// Generates a <see cref="BlazorFormDefinition"/> from a CLR type using reflection and DataAnnotations.
/// This gives "zero-config" forms from any POCO, while remaining fully customisable afterwards.
/// </summary>
public static class BlazorFormSchemaGenerator
{
    public static BlazorFormDefinition Generate<TModel>(BlazorFormSchemaGeneratorOptions? options = null)
        => Generate(typeof(TModel), options);

    public static BlazorFormDefinition Generate(Type modelType, BlazorFormSchemaGeneratorOptions? options = null)
    {
        options ??= new BlazorFormSchemaGeneratorOptions();
        var form = new BlazorFormDefinition { ModelType = modelType };
        foreach (var field in BuildFields(modelType, options, depth: 0, new HashSet<Type>()))
            form.Fields.Add(field);
        return form;
    }

    private static IEnumerable<BlazorFormFieldDefinition> BuildFields(
        Type type, BlazorFormSchemaGeneratorOptions options, int depth, HashSet<Type> ancestry)
    {
        var props = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .Where(p => !ShouldSkip(p, options))
            .OrderBy(p => p.MetadataToken);

        var list = new List<BlazorFormFieldDefinition>();
        foreach (var prop in props)
            list.Add(BuildField(prop, options, depth, ancestry));
        return list.OrderBy(f => f.Order);
    }

    private static BlazorFormFieldDefinition BuildField(
        PropertyInfo prop, BlazorFormSchemaGeneratorOptions options, int depth, HashSet<Type> ancestry)
    {
        var propType = prop.PropertyType;
        var fieldType = BlazorFormFieldTypeResolver.Resolve(propType);

        var field = new BlazorFormFieldDefinition(prop.Name, fieldType)
        {
            ValueType = propType,
            Label = BlazorFormFieldBuilder.Humanize(prop.Name)
        };

        var underlying = Nullable.GetUnderlyingType(propType) ?? propType;

        if (underlying.IsEnum)
        {
            field.Type = BlazorFormFieldType.Select;
            foreach (var name in Enum.GetNames(underlying))
                field.Options.Add(new BlazorFormSelectOption(name, BlazorFormFieldBuilder.Humanize(name)));
        }
        else if (fieldType == BlazorFormFieldType.Array && depth < options.MaxDepth)
        {
            var elementType = BlazorFormFieldTypeResolver.GetEnumerableElementType(propType) ?? typeof(string);
            field.ItemTemplate = BuildItemTemplate(elementType, options, depth + 1, ancestry);
        }
        else if (fieldType == BlazorFormFieldType.Object && depth < options.MaxDepth && !ancestry.Contains(underlying))
        {
            ancestry.Add(underlying);
            foreach (var child in BuildFields(underlying, options, depth + 1, ancestry))
                field.Children.Add(child);
            ancestry.Remove(underlying);
        }

        BlazorFormDataAnnotationsScanner.Apply(prop, field);
        return field;
    }

    private static BlazorFormFieldDefinition BuildItemTemplate(
        Type elementType, BlazorFormSchemaGeneratorOptions options, int depth, HashSet<Type> ancestry)
    {
        var elementFieldType = BlazorFormFieldTypeResolver.Resolve(elementType);
        if (elementFieldType == BlazorFormFieldType.Object && !ancestry.Contains(elementType))
        {
            var template = new BlazorFormFieldDefinition("item", BlazorFormFieldType.Object) { ValueType = elementType };
            ancestry.Add(elementType);
            foreach (var child in BuildFields(elementType, options, depth + 1, ancestry))
                template.Children.Add(child);
            ancestry.Remove(elementType);
            return template;
        }

        var scalar = new BlazorFormFieldDefinition("item", elementFieldType) { ValueType = elementType };
        var underlying = Nullable.GetUnderlyingType(elementType) ?? elementType;
        if (underlying.IsEnum)
        {
            scalar.Type = BlazorFormFieldType.Select;
            foreach (var name in Enum.GetNames(underlying))
                scalar.Options.Add(new BlazorFormSelectOption(name, BlazorFormFieldBuilder.Humanize(name)));
        }
        return scalar;
    }

    private static bool ShouldSkip(PropertyInfo prop, BlazorFormSchemaGeneratorOptions options)
    {
        if (prop.GetCustomAttribute<EditableAttribute>() is { AllowEdit: false } && prop.SetMethod is null)
            return true;
        if (options.HonorScaffoldColumn &&
            prop.GetCustomAttribute<ScaffoldColumnAttribute>() is { Scaffold: false })
            return true;
        if (prop.GetCustomAttribute<KeyAttribute>() is not null)
            return false;
        return false;
    }
}
