namespace BlazorForm;

/// <summary>Options controlling reflection-based schema generation.</summary>
public sealed class BlazorFormSchemaGeneratorOptions
{
    /// <summary>Maximum nesting depth for object graphs (guards against cycles).</summary>
    public int MaxDepth { get; set; } = 5;

    /// <summary>When true, properties marked <c>[ScaffoldColumn(false)]</c> are skipped.</summary>
    public bool HonorScaffoldColumn { get; set; } = true;
}
