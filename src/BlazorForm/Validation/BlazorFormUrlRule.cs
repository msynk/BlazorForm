namespace BlazorForm;

/// <summary>
/// Validates that the value is an absolute URL. Uses <see cref="Uri.TryCreate(string, UriKind, out Uri)"/>
/// rather than a regular expression so it accepts the same set of URLs the rest of .NET does.
/// </summary>
public sealed class BlazorFormUrlRule(string? message = null, params string[] allowedSchemes) : IBlazorFormValidationRule
{
    private readonly string[] _schemes = allowedSchemes.Length > 0
        ? allowedSchemes
        : [Uri.UriSchemeHttp, Uri.UriSchemeHttps];

    /// <inheritdoc />
    public string Key => "url";

    public ValueTask<BlazorFormRuleResult> ValidateAsync(BlazorFormValidationContext ctx)
    {
        var s = ctx.Value as string;
        if (string.IsNullOrWhiteSpace(s)) return new(BlazorFormRuleResult.Success());

        var ok = Uri.TryCreate(s, UriKind.Absolute, out var uri)
                 && _schemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase);

        return new(ok
            ? BlazorFormRuleResult.Success()
            : BlazorFormRuleResult.Fail(message ?? ctx.Message(BlazorFormMessageKeys.Url)));
    }
}
