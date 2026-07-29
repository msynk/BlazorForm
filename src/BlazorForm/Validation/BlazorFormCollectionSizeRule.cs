using System.Collections;

namespace BlazorForm;

/// <summary>Enforces minimum/maximum item counts on array/collection fields.</summary>
public sealed class BlazorFormCollectionSizeRule(int? min, int? max, string? message = null) : IBlazorFormValidationRule
{
    /// <inheritdoc />
    public string Key => "items";

    public ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext ctx)
    {
        var count = ctx.Value switch
        {
            null => 0,
            ICollection c => c.Count,
            IEnumerable e and not string => e.Cast<object?>().Count(),
            _ => 0
        };
        if (min.HasValue && count < min.Value)
            return new(BlazorFormRuleResult.Fail(message ?? ctx.Message(BlazorFormMessageKeys.MinItems, min)));
        if (max.HasValue && count > max.Value)
            return new(BlazorFormRuleResult.Fail(message ?? ctx.Message(BlazorFormMessageKeys.MaxItems, max)));
        return new(BlazorFormRuleResult.Success());
    }
}
