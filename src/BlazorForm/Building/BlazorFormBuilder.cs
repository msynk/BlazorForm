namespace BlazorForm;

/// <summary>
/// Untyped, schema-first form builder. Use this when you have no compiled model (e.g. when
/// assembling a form by hand or augmenting a JSON-imported schema).
/// </summary>
public class BlazorFormBuilder
{
    protected readonly BlazorFormDefinition Form = new();

    public static BlazorFormBuilder Create() => new();

    /// <summary>Starts a strongly-typed builder bound to <typeparamref name="TModel"/>.</summary>
    public static BlazorFormBuilder<TModel> For<TModel>() where TModel : class => new();

    public BlazorFormBuilder Title(string title) { Form.Title = title; return this; }
    public BlazorFormBuilder Description(string description) { Form.Description = description; return this; }

    /// <summary>Adds a field of the given type and configures it.</summary>
    public BlazorFormBuilder Field(string name, BlazorFormFieldType type, Action<BlazorFormFieldBuilder>? configure = null)
    {
        var def = new BlazorFormFieldDefinition(name, type) { Label = BlazorFormFieldBuilder.Humanize(name) };
        configure?.Invoke(new BlazorFormFieldBuilder(def));
        Form.Fields.Add(def);
        return this;
    }

    public BlazorFormBuilder Text(string name, Action<BlazorFormFieldBuilder>? configure = null) => Field(name, BlazorFormFieldType.Text, configure);
    public BlazorFormBuilder Number(string name, Action<BlazorFormFieldBuilder>? configure = null) => Field(name, BlazorFormFieldType.Number, configure);
    public BlazorFormBuilder Checkbox(string name, Action<BlazorFormFieldBuilder>? configure = null) => Field(name, BlazorFormFieldType.Checkbox, configure);
    public BlazorFormBuilder Select(string name, Action<BlazorFormFieldBuilder>? configure = null) => Field(name, BlazorFormFieldType.Select, configure);

    /// <summary>Adds an object (nested group) field.</summary>
    public BlazorFormBuilder Object(string name, Action<BlazorFormBuilder> children, Action<BlazorFormFieldBuilder>? configure = null)
    {
        var def = new BlazorFormFieldDefinition(name, BlazorFormFieldType.Object) { Label = BlazorFormFieldBuilder.Humanize(name) };
        var childBuilder = new BlazorFormBuilder();
        children(childBuilder);
        foreach (var c in childBuilder.Form.Fields) def.Children.Add(c);
        configure?.Invoke(new BlazorFormFieldBuilder(def));
        Form.Fields.Add(def);
        return this;
    }

    /// <summary>Adds a repeating array field whose items are described by <paramref name="item"/>.</summary>
    public BlazorFormBuilder Array(string name, Action<BlazorFormBuilder> item, Action<BlazorFormFieldBuilder>? configure = null)
    {
        var def = new BlazorFormFieldDefinition(name, BlazorFormFieldType.Array) { Label = BlazorFormFieldBuilder.Humanize(name) };
        var itemBuilder = new BlazorFormBuilder();
        item(itemBuilder);
        var template = new BlazorFormFieldDefinition("item", BlazorFormFieldType.Object);
        foreach (var c in itemBuilder.Form.Fields) template.Children.Add(c);
        def.ItemTemplate = template;
        configure?.Invoke(new BlazorFormFieldBuilder(def));
        Form.Fields.Add(def);
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

    public BlazorFormDefinition Build() => Form;
}
