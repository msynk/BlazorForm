using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BlazorForm.Core.Validation;

/// <summary>Fails when the value is null, empty or whitespace (or an empty collection).</summary>
public sealed class RequiredRule(string? message = null) : IValidationRule
{
    public ValueTask<RuleResult> ValidateAsync(ValidationContext ctx)
    {
        var empty = ctx.Value switch
        {
            null => true,
            string s => string.IsNullOrWhiteSpace(s),
            IEnumerable e and not string => !e.Cast<object?>().Any(),
            _ => false
        };
        return new(empty ? RuleResult.Fail(message ?? "This field is required.") : RuleResult.Success());
    }
}

/// <summary>Enforces a minimum string length.</summary>
public sealed class MinLengthRule(int min, string? message = null) : IValidationRule
{
    public ValueTask<RuleResult> ValidateAsync(ValidationContext ctx)
    {
        var s = ctx.Value as string;
        if (string.IsNullOrEmpty(s) || s.Length >= min)
            return new(RuleResult.Success());
        return new(RuleResult.Fail(message ?? $"Must be at least {min} characters."));
    }
}

/// <summary>Enforces a maximum string length.</summary>
public sealed class MaxLengthRule(int max, string? message = null) : IValidationRule
{
    public ValueTask<RuleResult> ValidateAsync(ValidationContext ctx)
    {
        var s = ctx.Value as string;
        if (s is null || s.Length <= max)
            return new(RuleResult.Success());
        return new(RuleResult.Fail(message ?? $"Must be at most {max} characters."));
    }
}

/// <summary>Enforces an inclusive numeric range.</summary>
public sealed class RangeRule(double? min, double? max, string? message = null) : IValidationRule
{
    public ValueTask<RuleResult> ValidateAsync(ValidationContext ctx)
    {
        if (ctx.Value is null) return new(RuleResult.Success());
        if (!TryToDouble(ctx.Value, out var d)) return new(RuleResult.Success());

        if (min.HasValue && d < min.Value)
            return new(RuleResult.Fail(message ?? RangeMessage()));
        if (max.HasValue && d > max.Value)
            return new(RuleResult.Fail(message ?? RangeMessage()));
        return new(RuleResult.Success());
    }

    private string RangeMessage() => (min, max) switch
    {
        ({ } lo, { } hi) => $"Must be between {lo} and {hi}.",
        ({ } lo, null) => $"Must be at least {lo}.",
        (null, { } hi) => $"Must be at most {hi}.",
        _ => "Out of range."
    };

    private static bool TryToDouble(object value, out double result)
        => double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
}

/// <summary>Validates against a regular expression.</summary>
public sealed class PatternRule(string pattern, string? message = null) : IValidationRule
{
    private readonly Regex _regex = new(pattern, RegexOptions.Compiled);

    public ValueTask<RuleResult> ValidateAsync(ValidationContext ctx)
    {
        var s = ctx.Value as string;
        if (string.IsNullOrEmpty(s) || _regex.IsMatch(s))
            return new(RuleResult.Success());
        return new(RuleResult.Fail(message ?? "Invalid format."));
    }
}

/// <summary>Validates an email address.</summary>
public sealed class EmailRule(string? message = null) : IValidationRule
{
    // Pragmatic email pattern; intentionally not RFC-perfect.
    private static readonly Regex Rx =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public ValueTask<RuleResult> ValidateAsync(ValidationContext ctx)
    {
        var s = ctx.Value as string;
        if (string.IsNullOrEmpty(s) || Rx.IsMatch(s))
            return new(RuleResult.Success());
        return new(RuleResult.Fail(message ?? "Enter a valid email address."));
    }
}

/// <summary>Enforces minimum/maximum item counts on array/collection fields.</summary>
public sealed class CollectionSizeRule(int? min, int? max, string? message = null) : IValidationRule
{
    public ValueTask<RuleResult> ValidateAsync(ValidationContext ctx)
    {
        var count = ctx.Value is IEnumerable e and not string ? e.Cast<object?>().Count() : 0;
        if (min.HasValue && count < min.Value)
            return new(RuleResult.Fail(message ?? $"Add at least {min} item(s)."));
        if (max.HasValue && count > max.Value)
            return new(RuleResult.Fail(message ?? $"No more than {max} item(s) allowed."));
        return new(RuleResult.Success());
    }
}

/// <summary>A synchronous custom rule backed by a delegate.</summary>
public sealed class DelegateRule(Func<ValidationContext, RuleResult> validate) : IValidationRule
{
    public ValueTask<RuleResult> ValidateAsync(ValidationContext ctx) => new(validate(ctx));
}

/// <summary>An asynchronous custom rule backed by a delegate (e.g. remote uniqueness checks).</summary>
public sealed class AsyncDelegateRule(Func<ValidationContext, ValueTask<RuleResult>> validate) : IValidationRule
{
    public bool IsAsync => true;
    public ValueTask<RuleResult> ValidateAsync(ValidationContext ctx) => validate(ctx);
}

/// <summary>
/// Wraps another rule so it only runs when <paramref name="condition"/> holds.
/// Used for conditional validation (e.g. "required when country == US").
/// </summary>
public sealed class ConditionalRule(IValidationRule inner, Func<ValidationContext, bool> condition) : IValidationRule
{
    public bool IsAsync => inner.IsAsync;

    public ValueTask<RuleResult> ValidateAsync(ValidationContext ctx)
        => condition(ctx) ? inner.ValidateAsync(ctx) : new(RuleResult.Success());
}
