using System.Collections;
using BlazorForm.Core.Data;
using BlazorForm.Core.Schema;

namespace BlazorForm.Core.Validation;

/// <summary>
/// Runs a <see cref="FormDefinition"/>'s validation rules against form data, walking nested
/// objects and array items and honouring conditional visibility (hidden fields are not validated).
/// </summary>
public sealed class FormValidator
{
    /// <summary>Validates the whole form (or a subset of top-level fields).</summary>
    /// <param name="form">The schema.</param>
    /// <param name="data">The data to validate.</param>
    /// <param name="services">Optional DI provider for rules that need it.</param>
    /// <param name="restrictToFields">When set, only these top-level field names are validated.</param>
    /// <param name="includeAsync">When false, async rules are skipped (e.g. for fast on-change validation).</param>
    public async ValueTask<IReadOnlyList<ValidationMessage>> ValidateAsync(
        FormDefinition form,
        IFormDataReader data,
        IServiceProvider? services = null,
        ISet<string>? restrictToFields = null,
        bool includeAsync = true)
    {
        var messages = new List<ValidationMessage>();
        foreach (var field in form.Fields)
        {
            if (restrictToFields is not null && !restrictToFields.Contains(field.Name))
                continue;
            await ValidateField(field, field.Name, data, services, includeAsync, messages);
        }
        return messages;
    }

    /// <summary>Validates a single field located at <paramref name="path"/>.</summary>
    public async ValueTask<IReadOnlyList<ValidationMessage>> ValidateFieldAsync(
        FieldDefinition field,
        string path,
        IFormDataReader data,
        IServiceProvider? services = null,
        bool includeAsync = true)
    {
        var messages = new List<ValidationMessage>();
        await ValidateField(field, path, data, services, includeAsync, messages);
        return messages;
    }

    private async ValueTask ValidateField(
        FieldDefinition field,
        string path,
        IFormDataReader data,
        IServiceProvider? services,
        bool includeAsync,
        List<ValidationMessage> messages)
    {
        // Hidden fields are skipped entirely (including their rules and children).
        if (field.VisibleWhen is not null && !field.VisibleWhen.Evaluate(data))
            return;

        var value = data.GetValue(path);

        foreach (var rule in field.Validators)
        {
            if (rule.IsAsync && !includeAsync)
                continue;

            var ctx = new ValidationContext(path, value, data, services);
            var result = await rule.ValidateAsync(ctx);
            if (!result.IsValid && result.Message is not null)
                messages.Add(new ValidationMessage(path, result.Message, result.Severity));
        }

        // Recurse into composition.
        if (field.Type == FieldType.Object)
        {
            foreach (var child in field.Children)
                await ValidateField(child, FormPath.Combine(path, child.Name), data, services, includeAsync, messages);
        }
        else if (field.Type == FieldType.Array && field.ItemTemplate is not null)
        {
            var count = value is IEnumerable e and not string ? e.Cast<object?>().Count() : 0;
            for (var i = 0; i < count; i++)
            {
                var itemPath = FormPath.Combine(path, i);
                var template = field.ItemTemplate;
                if (template.Type == FieldType.Object)
                {
                    foreach (var child in template.Children)
                        await ValidateField(child, FormPath.Combine(itemPath, child.Name), data, services, includeAsync, messages);
                }
                else
                {
                    await ValidateField(template, itemPath, data, services, includeAsync, messages);
                }
            }
        }
    }
}
