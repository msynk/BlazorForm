namespace BlazorForm;

/// <summary>How serious a <see cref="BlazorFormSchemaDiagnostic"/> is.</summary>
public enum BlazorFormSchemaDiagnosticSeverity
{
    /// <summary>The schema will render, but probably not the way its author meant.</summary>
    Warning,

    /// <summary>The schema is broken: something will throw, bind wrongly, or silently lose data.</summary>
    Error
}

/// <summary>
/// One problem found by <see cref="BlazorFormDefinition.Validate"/>.
/// </summary>
/// <param name="Path">Where in the schema the problem is, as a field path.</param>
/// <param name="Message">What is wrong, in terms of the fix.</param>
/// <param name="Severity">Whether the schema is merely suspect or actually broken.</param>
public sealed record BlazorFormSchemaDiagnostic(
    string Path,
    string Message,
    BlazorFormSchemaDiagnosticSeverity Severity = BlazorFormSchemaDiagnosticSeverity.Error)
{
    public override string ToString() => $"[{Severity}] {(Path.Length == 0 ? "(form)" : Path)}: {Message}";
}
