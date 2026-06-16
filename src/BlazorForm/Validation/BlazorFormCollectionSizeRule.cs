using System.Collections;

namespace BlazorForm;

/// <summary>Enforces minimum/maximum item counts on array/collection fields.</summary>
public sealed class BlazorFormCollectionSizeRule(int? min, int? max, string? message = null) : IBlazorFormValidationRule
{
    public ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext ctx)
    {
        var count = ctx.Value is IEnumerable e and not string ? e.Cast<object?>().Count() : 0;
        if (min.HasValue && count < min.Value)
            return new(BlazorFormRuleResult.Fail(message ?? $"Add at least {min} item(s)."));
        if (max.HasValue && count > max.Value)
            return new(BlazorFormRuleResult.Fail(message ?? $"No more than {max} item(s) allowed."));
        return new(BlazorFormRuleResult.Success());
    }
}
