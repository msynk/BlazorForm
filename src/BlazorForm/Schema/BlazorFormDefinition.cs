namespace BlazorForm;

/// <summary>
/// A complete form schema: the ordered set of fields, optional wizard steps and metadata.
/// A <see cref="BlazorFormDefinition"/> is the single source of truth a renderer consumes.
/// </summary>
public sealed class BlazorFormDefinition
{
    /// <summary>Optional form title.</summary>
    public string? Title { get; set; }

    /// <summary>Optional form description.</summary>
    public string? Description { get; set; }

    /// <summary>The CLR model type this schema was generated from, when applicable.</summary>
    public Type? ModelType { get; set; }

    /// <summary>
    /// Number of columns the form lays its fields out in. 1 (the default) is a single stacked column;
    /// higher values switch to a responsive grid that individual fields can span via
    /// <see cref="BlazorFormFieldDefinition.ColumnSpan"/>.
    /// </summary>
    public int Columns { get; set; } = 1;

    /// <summary>Top-level fields.</summary>
    public IList<BlazorFormFieldDefinition> Fields { get; set; } = new List<BlazorFormFieldDefinition>();

    /// <summary>Wizard steps. When non-empty, the form is multi-step.</summary>
    public IList<BlazorFormStep> Steps { get; set; } = new List<BlazorFormStep>();

    /// <summary>
    /// Rules validating the form as a whole rather than one field. Their messages are attached to the
    /// path each rule reports, so a cross-field rule can surface on the field the user should fix.
    /// </summary>
    public IList<IBlazorFormValidationRule> Validators { get; set; } = new List<IBlazorFormValidationRule>();

    /// <summary>True when the form is configured as a multi-step wizard.</summary>
    public bool IsWizard => Steps.Count > 0;

    /// <summary>Finds a top-level field by name.</summary>
    public BlazorFormFieldDefinition? FindField(string name)
        => Fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Resolves a field from a full data path such as <c>Address.City</c> or <c>Items[0].Product</c>,
    /// walking into object children and array item templates. Index segments select the item template,
    /// because every element of an array shares one definition.
    /// </summary>
    public BlazorFormFieldDefinition? FindByPath(string path)
    {
        // The typed builder can declare a field whose *name* is already a dotted path
        // (`x => x.Address.City`), so an exact match wins before the path is decomposed.
        var direct = Fields.FirstOrDefault(f => string.Equals(f.Name, path, StringComparison.OrdinalIgnoreCase));
        if (direct is not null) return direct;

        var segments = BlazorFormPath.Parse(path);
        BlazorFormFieldDefinition? current = null;

        foreach (var seg in segments)
        {
            if (seg.IsIndex)
            {
                // items[0] resolves to the template describing every element.
                current = current?.ItemTemplate;
                // An array of objects keeps its children on the template itself, so stay there.
                if (current is null) return null;
                continue;
            }

            var candidates = current is null
                ? Fields
                : current.Type == BlazorFormFieldType.Array && current.ItemTemplate is not null
                    ? current.ItemTemplate.Children
                    : current.Children;

            current = candidates.FirstOrDefault(f => string.Equals(f.Name, seg.Name, StringComparison.OrdinalIgnoreCase));
            if (current is null) return null;
        }

        return current;
    }

    /// <summary>Enumerates every field in the schema, recursing into objects and array item templates.</summary>
    public IEnumerable<BlazorFormFieldDefinition> AllFields()
    {
        foreach (var f in Fields)
        {
            yield return f;
            foreach (var d in f.Descendants()) yield return d;
        }
    }

    /// <summary>
    /// Top-level fields in the order they should be rendered. Ordering is stable: fields with the same
    /// <see cref="BlazorFormFieldDefinition.Order"/> keep their declared sequence.
    /// </summary>
    public IEnumerable<BlazorFormFieldDefinition> OrderedFields() => Fields.OrderBy(f => f.Order);

    /// <summary>
    /// Checks the schema for the mistakes that are easy to make and hard to see: two siblings sharing a
    /// name (they would bind to the same path and overwrite each other), an array with no item
    /// template, a step naming a field that does not exist, a condition or computed dependency pointing
    /// nowhere. Returns an empty list for a healthy schema.
    /// </summary>
    /// <remarks>
    /// This is a development aid, not a gate: it is never run automatically, because a schema assembled
    /// at runtime from user input has every right to be incomplete while it is being edited. Call it in
    /// a test, or behind a debug flag on a schema editor.
    /// </remarks>
    public IReadOnlyList<BlazorFormSchemaDiagnostic> Validate()
    {
        var diagnostics = new List<BlazorFormSchemaDiagnostic>();
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        CheckSiblings(Fields, string.Empty, diagnostics);
        foreach (var field in Fields) Walk(field, field.Name, diagnostics, known);

        foreach (var step in Steps)
        {
            if (string.IsNullOrWhiteSpace(step.Id))
                diagnostics.Add(new BlazorFormSchemaDiagnostic(string.Empty, "A wizard step has no id."));

            foreach (var name in step.Fields)
            {
                if (FindByPath(name) is null)
                    diagnostics.Add(new BlazorFormSchemaDiagnostic(name,
                        $"Step '{step.Id}' lists a field that is not in the schema; it will render nothing."));
            }
        }

        // Two fields both asking for focus is a race the schema cannot win: whichever renders last
        // takes it, so the answer changes with the layout rather than with the author's intent.
        var autofocused = AllFields().Where(f => f.Autofocus).Select(f => f.Name).ToList();
        if (autofocused.Count > 1)
            diagnostics.Add(new BlazorFormSchemaDiagnostic(autofocused[0],
                $"{autofocused.Count} fields are marked Autofocus ({string.Join(", ", autofocused)}); only one can win.",
                BlazorFormSchemaDiagnosticSeverity.Warning));

        var duplicateSteps = Steps.GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1);
        foreach (var group in duplicateSteps)
            diagnostics.Add(new BlazorFormSchemaDiagnostic(string.Empty,
                $"More than one wizard step has the id '{group.Key}'; GoToStep(id) will always find the first."));

        // Every field a step never mentions is still validated on submit but never shown, which reads
        // to the user as a form that refuses to submit for no visible reason.
        if (IsWizard)
        {
            var stepped = Steps.SelectMany(s => s.Fields).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var field in Fields.Where(f => f.Type != BlazorFormFieldType.Static && !stepped.Contains(f.Name)))
                diagnostics.Add(new BlazorFormSchemaDiagnostic(field.Name,
                    "This field belongs to no wizard step, so it is never shown — but it is still validated on submit.",
                    BlazorFormSchemaDiagnosticSeverity.Warning));
        }

        return diagnostics;

        void Walk(BlazorFormFieldDefinition field, string path,
            List<BlazorFormSchemaDiagnostic> into, HashSet<string> seen)
        {
            if (string.IsNullOrWhiteSpace(field.Name))
                into.Add(new BlazorFormSchemaDiagnostic(path, "A field has no name, so it binds to nothing."));

            if (field.Type == BlazorFormFieldType.Array && field.ItemTemplate is null)
                into.Add(new BlazorFormSchemaDiagnostic(path,
                    "An array field has no ItemTemplate; rendering it will throw. Use the builder's Array(...) or ArrayOf(...) overload."));

            if (field.Type == BlazorFormFieldType.Custom && string.IsNullOrEmpty(field.CustomRenderer))
                into.Add(new BlazorFormSchemaDiagnostic(path,
                    "A custom field has no renderer key, so no component can be resolved for it."));

            if (field.IsChoice && field.Options.Count == 0 && field.OptionsProvider is null)
                into.Add(new BlazorFormSchemaDiagnostic(path,
                    "A choice field has neither options nor an options provider; it will render an empty list.",
                    BlazorFormSchemaDiagnosticSeverity.Warning));

            // The provider always wins, so the static list is never rendered — but it is still there in
            // the schema, and reading it is the natural way to be wrong about what the field offers.
            if (field.OptionsProvider is not null && field.Options.Count > 0)
                into.Add(new BlazorFormSchemaDiagnostic(path,
                    "A field has both static options and an options provider; the provider wins and the static list is never shown.",
                    BlazorFormSchemaDiagnosticSeverity.Warning));

            // Bounds that cross over describe a field no value can satisfy, so the form can never be
            // submitted and nothing on the page says why. Almost always a transposed pair of arguments.
            CheckBounds(field.MinLength, field.MaxLength, "MinLength", "MaxLength", path, into);
            CheckBounds(field.MinItems, field.MaxItems, "MinItems", "MaxItems", path, into);
            CheckBounds(field.Min, field.Max, "Min", "Max", path, into);

            if (field.Computed is not null && !field.ReadOnly)
                into.Add(new BlazorFormSchemaDiagnostic(path,
                    "A computed field is editable, so anything typed into it is overwritten the next time a dependency changes.",
                    BlazorFormSchemaDiagnosticSeverity.Warning));

            // A confirm-password rule pointing at nothing is silently satisfied: the other value reads
            // as null, this one is compared against it, and the field passes for the wrong reason.
            foreach (var compare in field.Validators.OfType<BlazorFormCompareRule>())
                CheckPaths([compare.OtherPath], path, "MatchesField", into);

            CheckPaths(field.VisibleWhen?.Dependencies, path, "VisibleWhen", into);
            CheckPaths(field.DisabledWhen?.Dependencies, path, "DisabledWhen", into);
            CheckPaths(field.RequiredWhen?.Dependencies, path, "RequiredWhen", into);
            CheckPaths(field.ComputedDependencies, path, "a computed dependency", into);
            CheckPaths(field.OptionsDependencies, path, "an options dependency", into);

            seen.Add(path);

            if (field.Type == BlazorFormFieldType.Object)
            {
                CheckSiblings(field.Children, path, into);
                foreach (var child in field.Children)
                    Walk(child, BlazorFormPath.Combine(path, child.Name), into, seen);
            }
            else if (field.ItemTemplate is { } template)
            {
                var itemPath = path + "[]";
                if (template.Type == BlazorFormFieldType.Object)
                {
                    CheckSiblings(template.Children, itemPath, into);
                    foreach (var child in template.Children)
                        Walk(child, BlazorFormPath.Combine(itemPath, child.Name), into, seen);
                }
                else
                {
                    Walk(template, itemPath, into, seen);
                }
            }
        }

        static void CheckBounds<T>(T? min, T? max, string minName, string maxName, string path,
            List<BlazorFormSchemaDiagnostic> into) where T : struct, IComparable<T>
        {
            if (min is { } lo && max is { } hi && lo.CompareTo(hi) > 0)
                into.Add(new BlazorFormSchemaDiagnostic(path,
                    $"{minName} ({lo}) is greater than {maxName} ({hi}); no value can satisfy both."));
        }

        void CheckPaths(IEnumerable<string>? paths, string owner, string what, List<BlazorFormSchemaDiagnostic> into)
        {
            if (paths is null) return;
            var scope = BlazorFormPath.Parent(owner.Replace("[]", "[0]", StringComparison.Ordinal));

            foreach (var dependency in paths)
            {
                if (string.IsNullOrEmpty(dependency)) continue;
                // Resolved absolutely or relative to the owning object, exactly as it is at run time.
                if (FindByPath(dependency) is not null) continue;
                if (scope.Length > 0 && FindByPath(BlazorFormPath.Combine(scope, dependency)) is not null) continue;

                into.Add(new BlazorFormSchemaDiagnostic(owner,
                    $"{what} refers to '{dependency}', which is not a field in this schema.",
                    BlazorFormSchemaDiagnosticSeverity.Warning));
            }
        }

        static void CheckSiblings(IEnumerable<BlazorFormFieldDefinition> fields, string parent,
            List<BlazorFormSchemaDiagnostic> into)
        {
            foreach (var group in fields.GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                into.Add(new BlazorFormSchemaDiagnostic(BlazorFormPath.Combine(parent, group.Key),
                    $"{group.Count()} sibling fields share the name '{group.Key}'; they bind to the same path and will overwrite each other."));
        }
    }
}
