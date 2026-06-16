using BlazorForm.Core.Schema;
using BlazorForm.Rendering;
using Microsoft.AspNetCore.Components;

namespace BlazorForm.Components.Inputs;

/// <summary>Base class shared by all built-in field input components.</summary>
public abstract class InputBase : ComponentBase
{
    /// <summary>The render context for the field being displayed.</summary>
    [Parameter, EditorRequired] public FieldContext Context { get; set; } = default!;

    protected FieldDefinition Field => Context.Field;
    protected bool Disabled => Context.IsDisabled;

    protected string CssClass => Context.HasError && Context.ShowMessages
        ? "ff-input ff-input--invalid"
        : "ff-input";

    protected Task OnChange(ChangeEventArgs e) => Context.SetFromStringAsync(e.Value?.ToString());
    protected Task OnBlur() => Context.ValidateAsync();
}
