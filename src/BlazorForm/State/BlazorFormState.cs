using System.Collections;
using System.Reflection;

namespace BlazorForm;

/// <summary>
/// Runtime state for a form instance: holds the data, validation results, touched/dirty tracking,
/// wizard position and submission state. The UI layer binds to this and reacts to <see cref="StateChanged"/>.
/// </summary>
public sealed class BlazorFormState : IDisposable
{
    private readonly BlazorFormValidator _validator = new();
    private readonly Dictionary<string, List<BlazorFormValidationMessage>> _messages = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _touched = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _dirty = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _initialValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<BlazorFormSelectOption>> _loadedOptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _optionsInFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CancellationTokenSource> _fieldValidationCts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CancellationTokenSource> _optionsCts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _conversionErrors = new(StringComparer.OrdinalIgnoreCase);

    private readonly bool _hasClearOnHide;
    private readonly bool _hasComputed;
    private CancellationTokenSource? _formValidationCts;
    private bool _readOnly;
    private bool _disposed;

    public BlazorFormState(BlazorFormDefinition definition, IBlazorFormDataAccessor data, IServiceProvider? services = null)
    {
        Definition = definition;
        Data = data;
        Services = services;
        // Checked once: these sweeps run after every value change, and most schemas use neither.
        _hasClearOnHide = definition.AllFields().Any(f => f.ClearOnHide);
        _hasComputed = definition.AllFields().Any(f => f.Computed is not null);

        ApplyDefaults();
        SeedRequiredArrayItems();
        RecomputeValues(changedPath: null);
        CaptureInitialValues();

        // Seeding the form is not the user changing it. Without this a schema with any computed field
        // reports IsFormDirty the instant it is constructed, so an "undo" button bound to it is enabled
        // on a form nobody has touched.
        _dirty.Clear();
    }

    /// <summary>The schema being rendered.</summary>
    public BlazorFormDefinition Definition { get; }

    /// <summary>The data store (typed model or dictionary).</summary>
    public IBlazorFormDataAccessor Data { get; }

    /// <summary>Optional service provider for validators that need DI.</summary>
    public IServiceProvider? Services { get; }

    /// <summary>Optional external validator merged with built-in rules (set by integrations).</summary>
    public BlazorFormExternalValidator? ExternalValidator { get; set; }

    /// <summary>
    /// Makes every field read-only at once — the "review before you submit" or "view an existing
    /// record" mode, without needing a second schema.
    /// </summary>
    public bool ReadOnly
    {
        get => _readOnly;
        set
        {
            if (_readOnly == value) return;
            _readOnly = value;
            NotifyChanged();
        }
    }

    /// <summary>When a field revalidates before the form has been submitted. Defaults to <see cref="BlazorFormValidationTrigger.OnChange"/>.</summary>
    public BlazorFormValidationTrigger ValidationTrigger { get; set; } = BlazorFormValidationTrigger.OnChange;

    /// <summary>
    /// When a field revalidates once the form has been submitted at least once, or the field already
    /// shows an error. Defaults to <see cref="BlazorFormValidationTrigger.OnChange"/> so a corrected
    /// field clears its error immediately.
    /// </summary>
    public BlazorFormValidationTrigger RevalidationTrigger { get; set; } = BlazorFormValidationTrigger.OnChange;

    /// <summary>Index of the active wizard step (ignored for non-wizard forms).</summary>
    public int CurrentStepIndex { get; private set; }

    /// <summary>Number of times submission has been attempted.</summary>
    public int SubmitCount { get; private set; }

    /// <summary>True while an async submit/validation is in flight.</summary>
    public bool IsValidating { get; private set; }

    /// <summary>True while the consumer's submit handler is running. Used to block double submits.</summary>
    public bool IsSubmitting { get; private set; }

    /// <summary>True once the form has been submitted at least once.</summary>
    public bool IsSubmitted => SubmitCount > 0;

    /// <summary>
    /// True when no error-severity message is currently recorded.
    /// </summary>
    /// <remarks>
    /// This reports what validation has <em>found</em>, not what it would find: a form that has never
    /// been validated has no messages and so reports valid, even with a required field left blank.
    /// Check <see cref="HasValidated"/> before treating it as a verdict — binding a submit button's
    /// <c>disabled</c> to <c>IsValid</c> alone would leave the button enabled on an empty form and then
    /// disable it the moment the user's first mistake is caught, which is exactly backwards.
    /// Prefer letting the user submit and reporting what is wrong.
    /// </remarks>
    public bool IsValid => !HasErrors;

    /// <summary>
    /// True once a full validation pass has run at least once, so <see cref="IsValid"/> reflects a real
    /// verdict rather than the absence of one. Reset by <see cref="Reset"/>.
    /// </summary>
    public bool HasValidated { get; private set; }

    /// <summary>Raised whenever state changes and the UI should re-render.</summary>
    public event Action? StateChanged;

    /// <summary>Raised when a specific field value changes, with its path.</summary>
    public event Action<string>? FieldChanged;

    // ---------------------------------------------------------------- values

    public object? GetValue(string path) => Data.GetValue(path);

    public T? GetValue<T>(string path)
    {
        var v = Data.GetValue(path);
        if (v is null) return default;
        if (v is T t) return t;
        return BlazorFormValueConverter.TryCoerce(v, typeof(T), out var converted) && converted is T c ? c : default;
    }

    /// <summary>Sets a value, marks the field dirty and touched, and notifies listeners.</summary>
    public void SetValue(string path, object? value) => Write(path, value, markTouched: true);

    /// <summary>
    /// Sets a value without marking the field touched — for programmatic updates (computed values,
    /// prefill) that should not make the user's untouched field start showing errors.
    /// </summary>
    public void SetValueQuietly(string path, object? value) => Write(path, value, markTouched: false);

    /// <summary>
    /// Writes several values as one change: listeners are notified once at the end rather than once per
    /// field. Prefilling a form from a saved record otherwise costs one re-render per value, and any
    /// intermediate state — half the answers applied — is briefly visible to a computed field or a
    /// condition watching from the outside.
    /// </summary>
    /// <param name="values">Path/value pairs to write.</param>
    /// <param name="markTouched">
    /// Whether the fields count as touched. False (the default) is right for prefilling: the user has
    /// not visited these fields, so they should not start out covered in errors.
    /// </param>
    public void SetValues(IEnumerable<KeyValuePair<string, object?>> values, bool markTouched = false)
    {
        ArgumentNullException.ThrowIfNull(values);
        Batch(() =>
        {
            foreach (var (path, value) in values) Write(path, value, markTouched);
        });
    }

    /// <summary>
    /// Runs <paramref name="action"/> with change notifications held back, raising a single one at the
    /// end. Nesting is safe; only the outermost batch notifies. An exception still releases the batch,
    /// so a failure part-way through cannot leave the form permanently silent.
    /// </summary>
    public void Batch(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        _batchDepth++;
        try
        {
            action();
        }
        finally
        {
            _batchDepth--;
            if (_batchDepth == 0 && _batchChanged)
            {
                _batchChanged = false;
                NotifyChanged();
            }
        }
    }

    private int _batchDepth;
    private bool _batchChanged;

    private void Write(string path, object? value, bool markTouched)
    {
        Data.SetValue(path, value);
        RecordConversionResult(path, value);

        _dirty.Add(path);
        if (markTouched) _touched.Add(path);

        // Side effects first, so a listener sees the form in its settled state.
        ClearHiddenValues(path);
        RecomputeValues(path);
        InvalidateDependentOptions(path);
        // The answer just given may have hidden the step the user is standing on.
        ClampStep();
        FieldChanged?.Invoke(path);
        NotifyChanged();
    }

    /// <summary>
    /// Refreshes every computed field that reads <paramref name="changedPath"/>. Passing null
    /// recomputes them all, which is what the constructor needs.
    /// </summary>
    /// <remarks>
    /// A computed field may itself feed another, so each update cascades — bounded by
    /// <paramref name="depth"/> so a schema whose formulas reference each other in a cycle settles
    /// instead of recursing forever.
    /// </remarks>
    private void RecomputeValues(string? changedPath, int depth = 0)
    {
        const int maxCascade = 8;
        if (!_hasComputed || depth > maxCascade) return;

        foreach (var (field, path) in EnumerateFieldPaths())
        {
            if (field.Computed is null) continue;

            var scope = BlazorFormPath.Parent(path);
            if (changedPath is not null && !ReadsPath(field.ComputedDependencies, changedPath, scope)) continue;
            // A formula that reads its own output would otherwise re-trigger itself forever.
            if (string.Equals(path, changedPath, StringComparison.OrdinalIgnoreCase)) continue;

            var next = field.Computed(new BlazorFormComputedContext(Data, path, scope));
            if (Equals(Data.GetValue(path), next)) continue;

            Data.SetValue(path, next);
            _dirty.Add(path);
            RecomputeValues(path, depth + 1);
        }
    }

    /// <summary>
    /// Whether a declared dependency list covers <paramref name="path"/>. Dependencies are matched
    /// both absolutely and relative to <paramref name="scope"/>, so a formula on an array item can
    /// name its siblings (<c>"Quantity"</c>) without knowing which row it will end up on. An empty
    /// list means "depends on everything", matching how conditions declare their dependencies.
    /// </summary>
    private static bool ReadsPath(IList<string> dependencies, string path, string scope)
    {
        if (dependencies.Count == 0) return true;

        foreach (var dependency in dependencies)
        {
            if (BlazorFormPath.IsAtOrUnder(path, dependency)) return true;
            if (scope.Length > 0 && BlazorFormPath.IsAtOrUnder(path, BlazorFormPath.Combine(scope, dependency)))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Turns a rejected write into a validation message. Without this the model silently keeps its
    /// previous value and the user's entry disappears on the next render with no explanation.
    /// </summary>
    private void RecordConversionResult(string path, object? attempted)
    {
        if (Data.LastWriteFailed)
        {
            _conversionErrors[path] = BlazorFormValueConverter.ToInvariantString(attempted);
            // Whatever else was reported about this field described a value the model never took, and
            // typing a second bad entry must not stack a second copy of the same complaint.
            RemoveMessagesFor(path);
            AddMessage(ConversionMessage(path));
        }
        else if (_conversionErrors.Remove(path))
        {
            RemoveMessagesFor(path);
        }
    }

    private BlazorFormValidationMessage ConversionMessage(string path)
        => new(path, Text(BlazorFormMessageKeys.Conversion, _conversionErrors[path]));

    /// <summary>
    /// Resolves a piece of the library's own text (see <see cref="BlazorFormMessageKeys"/>) through the
    /// registered <see cref="IBlazorFormMessageProvider"/>, falling back to the English defaults. The
    /// built-in components render everything through this, so a single provider translates the whole
    /// form — buttons, placeholders and repeater labels included, not only the validation messages.
    /// </summary>
    public string Text(string key, params object?[] args) => Messages.Get(key, args);

    // Resolved once. A repeater asks for four labels per row per render, and a container lookup for
    // each of them is pure waste — the registration cannot change for the life of a form anyway.
    private IBlazorFormMessageProvider? _messageProvider;

    private IBlazorFormMessageProvider Messages
        => _messageProvider ??= Services?.GetService(typeof(IBlazorFormMessageProvider)) as IBlazorFormMessageProvider
                                ?? BlazorFormDefaultMessageProvider.Instance;

    /// <summary>
    /// Re-attaches conversion messages after a validation pass has rebuilt the message set, so a value
    /// the model could never accept keeps reporting itself.
    /// </summary>
    private void ReapplyConversionErrors(string? onlyPath = null)
    {
        foreach (var path in _conversionErrors.Keys)
        {
            if (onlyPath is not null && !string.Equals(path, onlyPath, StringComparison.OrdinalIgnoreCase)) continue;
            AddMessage(ConversionMessage(path));
        }
    }

    public bool IsTouched(string path) => _touched.Contains(path);
    public bool IsDirty(string path) => _dirty.Contains(path);
    public bool IsFormDirty => _dirty.Count > 0;

    /// <summary>Every field path the user has interacted with.</summary>
    public IReadOnlyCollection<string> TouchedFields => _touched;

    /// <summary>Every field path whose value has changed since the form was created or last reset.</summary>
    public IReadOnlyCollection<string> DirtyFields => _dirty;

    public void MarkTouched(string path)
    {
        if (_touched.Add(path)) NotifyChanged();
    }

    /// <summary>
    /// Marks every field touched, so validation messages become visible everywhere. Called on submit
    /// so a user who submits an untouched form still sees what is missing.
    /// </summary>
    public void MarkAllTouched()
    {
        foreach (var path in EnumerateValuePaths())
            _touched.Add(path);
        foreach (var key in _messages.Keys)
            _touched.Add(key);
        NotifyChanged();
    }

    // ---------------------------------------------------------------- conditional state

    /// <summary>
    /// Whether a field is currently visible given the data. Pass <paramref name="path"/> so a condition
    /// written on an array item's template is evaluated against that row; omitting it evaluates against
    /// the root, which is all a top-level field ever needs.
    /// </summary>
    public bool IsVisible(BlazorFormFieldDefinition field, string? path = null)
        => field.VisibleWhen is null || field.VisibleWhen.Evaluate(ScopeFor(path));

    /// <summary>
    /// Whether a field is currently disabled given the data. Read-only fields are deliberately *not*
    /// disabled — a disabled control is skipped by keyboard navigation and unreadable to screen
    /// readers, so read-only is rendered with the <c>readonly</c> attribute instead. See <see cref="IsReadOnly"/>.
    /// </summary>
    public bool IsDisabled(BlazorFormFieldDefinition field, string? path = null)
        => field.DisabledWhen is not null && field.DisabledWhen.Evaluate(ScopeFor(path));

    /// <summary>Whether a field is read-only, either in its own right or because the whole form is.</summary>
    public bool IsReadOnly(BlazorFormFieldDefinition field) => ReadOnly || field.ReadOnly;

    /// <summary>Whether a field is required right now, taking <see cref="BlazorFormFieldDefinition.RequiredWhen"/> into account.</summary>
    public bool IsRequired(BlazorFormFieldDefinition field, string? path = null)
        => field.Required || (field.RequiredWhen is not null && field.RequiredWhen.Evaluate(ScopeFor(path)));

    /// <summary>Data scoped to the container that owns <paramref name="path"/>, or the root when it has none.</summary>
    private IBlazorFormDataReader ScopeFor(string? path)
        => path is null ? Data : BlazorFormScopedDataReader.ForOwnerOf(Data, path);

    /// <summary>Whether a wizard step is currently visible.</summary>
    public bool IsStepVisible(BlazorFormStep step)
        => step.VisibleWhen is null || step.VisibleWhen.Evaluate(Data);

    /// <summary>
    /// Clears the value of every field marked <see cref="BlazorFormFieldDefinition.ClearOnHide"/> that
    /// has just become invisible, so a hidden branch never contributes stale data to the model.
    /// Only fields whose condition actually reads <paramref name="changedPath"/> are re-examined.
    /// </summary>
    private void ClearHiddenValues(string changedPath)
    {
        if (!_hasClearOnHide) return;

        foreach (var (field, path) in EnumerateFieldPaths())
        {
            if (!field.ClearOnHide || field.VisibleWhen is null) continue;
            if (!DependsOn(field.VisibleWhen, changedPath, BlazorFormPath.Parent(path))) continue;
            if (field.VisibleWhen.Evaluate(ScopeFor(path))) continue;
            if (Data.GetValue(path) is null) continue;

            Data.SetValue(path, null);
            RemoveMessagesUnder(path);
            _touched.Remove(path);
            // Emptying a field is a change to the data like any other, so the form is now dirty even
            // though the user never typed in this box.
            _dirty.Add(path);
        }
    }

    /// <summary>
    /// Whether a condition reads <paramref name="path"/>. Dependencies are matched both absolutely and
    /// relative to <paramref name="scope"/>, mirroring how the condition itself is evaluated. A
    /// condition that declares no dependencies — a raw predicate, say — is assumed to depend on
    /// everything, matching <see cref="IBlazorFormCondition.Dependencies"/>.
    /// </summary>
    private static bool DependsOn(IBlazorFormCondition condition, string path, string scope = "")
    {
        var any = false;
        foreach (var dependency in condition.Dependencies)
        {
            any = true;
            if (BlazorFormPath.IsAtOrUnder(path, dependency)) return true;
            if (scope.Length > 0 && BlazorFormPath.IsAtOrUnder(path, BlazorFormPath.Combine(scope, dependency)))
                return true;
        }
        return !any;
    }

    // ---------------------------------------------------------------- options

    /// <summary>
    /// The options to render for a field: those loaded by its
    /// <see cref="BlazorFormFieldDefinition.OptionsProvider"/> if it has one, otherwise the static
    /// <see cref="BlazorFormFieldDefinition.Options"/> from the schema.
    /// </summary>
    public IReadOnlyList<BlazorFormSelectOption> OptionsFor(BlazorFormFieldDefinition field, string path)
    {
        if (field.OptionsProvider is not null)
            return _loadedOptions.TryGetValue(path, out var loaded) ? loaded : Array.Empty<BlazorFormSelectOption>();

        // The schema's own list is handed back as-is when it already is a read-only list (the default),
        // because this sits on the render path and copying it per render would be pure waste.
        return field.Options as IReadOnlyList<BlazorFormSelectOption> ?? field.Options.ToList();
    }

    /// <summary>True while a field's <see cref="BlazorFormFieldDefinition.OptionsProvider"/> is running.</summary>
    public bool IsLoadingOptions(string path) => _optionsInFlight.Contains(path);

    /// <summary>
    /// Runs a field's options provider unless its results are already cached. Renderers call this on
    /// first render and after a dependency changes; it is a no-op for fields with static options.
    /// </summary>
    public async ValueTask EnsureOptionsAsync(BlazorFormFieldDefinition field, string path)
    {
        if (field.OptionsProvider is null || _loadedOptions.ContainsKey(path) || !_optionsInFlight.Add(path))
            return;

        var cts = new CancellationTokenSource();
        _optionsCts[path] = cts;

        NotifyChanged();
        try
        {
            var ctx = new BlazorFormOptionsContext(field, path, Data, Services, cts.Token);
            var options = await field.OptionsProvider(ctx);
            if (!cts.IsCancellationRequested) _loadedOptions[path] = options;
        }
        catch (OperationCanceledException)
        {
            // A newer load (or disposal) superseded this one; the cache stays empty so it retries.
        }
        finally
        {
            _optionsInFlight.Remove(path);
            if (_optionsCts.TryGetValue(path, out var current) && ReferenceEquals(current, cts))
            {
                _optionsCts.Remove(path);
                cts.Dispose();
            }
            NotifyChanged();
        }
    }

    /// <summary>Drops cached options for a field so the next render reloads them.</summary>
    public void InvalidateOptions(string path)
    {
        CancelOptionsLoad(path);
        if (_loadedOptions.Remove(path)) NotifyChanged();
    }

    /// <summary>Aborts an in-flight options load, so a superseded lookup stops doing work.</summary>
    private void CancelOptionsLoad(string path)
    {
        if (!_optionsCts.Remove(path, out var cts)) return;
        cts.Cancel();
        cts.Dispose();
        _optionsInFlight.Remove(path);
    }

    private void InvalidateDependentOptions(string changedPath)
    {
        if (_loadedOptions.Count == 0) return;

        foreach (var (field, path) in EnumerateFieldPaths())
        {
            if (field.OptionsProvider is null || !_loadedOptions.ContainsKey(path)) continue;
            if (!field.OptionsDependencies.Any(d => string.Equals(d, changedPath, StringComparison.OrdinalIgnoreCase)))
                continue;

            CancelOptionsLoad(path);
            _loadedOptions.Remove(path);
            // The previously selected option may no longer exist in the new list.
            if (Data.GetValue(path) is not null) Data.SetValue(path, null);
        }
    }

    // ---------------------------------------------------------------- validation

    /// <summary>Records a submit attempt (so validation messages become visible) and returns the count.</summary>
    public int RegisterSubmitAttempt()
    {
        SubmitCount++;
        return SubmitCount;
    }

    /// <summary>All current validation messages across the form.</summary>
    public IEnumerable<BlazorFormValidationMessage> AllMessages => _messages.Values.SelectMany(x => x);

    /// <summary>
    /// All current messages in the order their fields appear in the schema — the order an error
    /// summary should list them so "fix the first one" walks the user down the page.
    /// </summary>
    public IReadOnlyList<BlazorFormValidationMessage> OrderedMessages()
    {
        // Ordering walks the whole schema, including every live array row. A valid form is the usual
        // case and has nothing to order, so it never pays for the walk.
        if (_messages.Count == 0) return Array.Empty<BlazorFormValidationMessage>();

        var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var i = 0;
        foreach (var (_, path) in EnumerateFieldPaths())
            order.TryAdd(path, i++);

        return AllMessages
            .OrderBy(m => order.TryGetValue(m.FieldPath, out var idx) ? idx : int.MaxValue)
            .ThenBy(m => m.FieldPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Messages for a specific field path.</summary>
    public IReadOnlyList<BlazorFormValidationMessage> MessagesFor(string path)
        => _messages.TryGetValue(path, out var list) ? list : Array.Empty<BlazorFormValidationMessage>();

    /// <summary>True if there is at least one error-severity message.</summary>
    public bool HasErrors => AllMessages.Any(m => m.Severity == BlazorFormValidationSeverity.Error);

    /// <summary>Validates the entire form and stores the results.</summary>
    public async ValueTask<bool> ValidateAsync(bool includeAsync = true)
    {
        // A newer full validation always wins over one still in flight, so a slow async rule can never
        // overwrite results computed from more recent data.
        _formValidationCts?.Cancel();
        _formValidationCts?.Dispose();
        var cts = _formValidationCts = new CancellationTokenSource();

        IsValidating = true;
        NotifyChanged();
        try
        {
            var messages = await _validator.ValidateAsync(Definition, Data, Services, null, includeAsync, cts.Token);
            var merged = await MergeExternal(messages);
            cts.Token.ThrowIfCancellationRequested();
            ReplaceAllMessages(DropHiddenStepMessages(merged));
            ReapplyConversionErrors();
            HasValidated = true;
            return !HasErrors;
        }
        catch (OperationCanceledException)
        {
            return !HasErrors;
        }
        finally
        {
            if (ReferenceEquals(_formValidationCts, cts))
            {
                IsValidating = false;
                _formValidationCts = null;
                cts.Dispose();
            }
            NotifyChanged();
        }
    }

    /// <summary>Validates the fields of the current wizard step.</summary>
    public async ValueTask<bool> ValidateStepAsync(bool includeAsync = true)
    {
        if (!Definition.IsWizard) return await ValidateAsync(includeAsync);

        // Through CurrentStep, so a step a condition has just hidden is never the one being validated.
        var step = CurrentStep!;

        // Each step field is resolved as a path rather than a top-level name, so a step can own a
        // nested field ("Address.City") just as easily as a root one.
        var messages = new List<BlazorFormValidationMessage>();
        foreach (var path in step.Fields)
        {
            if (Definition.FindByPath(path) is not { } field) continue;
            messages.AddRange(await _validator.ValidateFieldAsync(field, path, Data, Services, includeAsync));
        }

        // External validators (FluentValidation and friends) see the whole model, so their results are
        // filtered down to the fields this step owns — otherwise a later step's errors would block it.
        if (ExternalValidator is not null)
        {
            var external = await ExternalValidator(Definition, Data, Services);
            messages.AddRange(external.Where(m =>
                step.Fields.Any(f => BlazorFormPath.IsAtOrUnder(m.FieldPath, f)) && !IsHidden(m.FieldPath)));
        }

        // Replace only messages for fields in this step, and mark them touched so the errors show.
        foreach (var path in step.Fields)
        {
            RemoveMessagesUnder(path);
            _touched.Add(path);
        }
        foreach (var m in messages)
        {
            AddMessage(m);
            // A step field may be an object or array, so touch the exact path each message landed on.
            _touched.Add(m.FieldPath);
        }

        // A value the model could never accept must block the step too, even though no rule produced it.
        var blocked = false;
        foreach (var path in _conversionErrors.Keys.Where(p => step.Fields.Any(f => BlazorFormPath.IsAtOrUnder(p, f))))
        {
            AddMessage(ConversionMessage(path));
            _touched.Add(path);
            blocked = true;
        }

        NotifyChanged();
        return !blocked && !messages.Any(m => m.Severity == BlazorFormValidationSeverity.Error);
    }

    /// <summary>Validates a single field and refreshes only its messages.</summary>
    public async ValueTask ValidateFieldAsync(BlazorFormFieldDefinition field, string path, bool includeAsync = true)
    {
        // Supersede any in-flight validation of the same field so results always reflect the latest value.
        if (_fieldValidationCts.TryGetValue(path, out var previous))
        {
            previous.Cancel();
            previous.Dispose();
        }
        var cts = new CancellationTokenSource();
        _fieldValidationCts[path] = cts;

        try
        {
            var messages = await _validator.ValidateFieldAsync(field, path, Data, Services, includeAsync, cts.Token);
            if (cts.IsCancellationRequested) return;

            RemoveMessagesUnder(path);
            foreach (var m in messages) AddMessage(m);
            ReapplyConversionErrors(path);
            NotifyChanged();
        }
        catch (OperationCanceledException)
        {
            // Superseded.
        }
        finally
        {
            if (_fieldValidationCts.TryGetValue(path, out var current) && ReferenceEquals(current, cts))
            {
                _fieldValidationCts.Remove(path);
                cts.Dispose();
            }
        }
    }

    /// <summary>
    /// Whether a change (or blur) on <paramref name="path"/> should trigger validation, given the
    /// configured triggers. A field that already shows an error always revalidates eagerly so the
    /// error disappears as soon as it is fixed.
    /// </summary>
    public bool ShouldValidate(string path, BlazorFormValidationTrigger trigger)
    {
        var effective = IsSubmitted || MessagesFor(path).Count > 0 ? RevalidationTrigger : ValidationTrigger;
        return effective switch
        {
            BlazorFormValidationTrigger.OnChange => trigger is BlazorFormValidationTrigger.OnChange or BlazorFormValidationTrigger.OnBlur,
            BlazorFormValidationTrigger.OnBlur => trigger is BlazorFormValidationTrigger.OnBlur,
            _ => false
        };
    }

    // ---------------------------------------------------------------- focus

    private readonly Dictionary<string, Func<ValueTask<bool>>> _focusTargets = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a way to move focus to the control rendering <paramref name="path"/>. Input components
    /// call this so the state can honour "take me to the first error" without knowing anything about
    /// the DOM. The callback reports whether focus actually landed. Dispose the returned token to
    /// unregister.
    /// </summary>
    public IDisposable RegisterFocusTarget(string path, Func<ValueTask<bool>> focus)
    {
        _focusTargets[path] = focus;
        return new FocusRegistration(this, path, focus);
    }

    /// <summary>
    /// Moves focus to the control at <paramref name="path"/>. Returns false when nothing is rendering
    /// it — a field on another wizard step, one a condition has hidden, or a control with no focusable
    /// element of its own.
    /// </summary>
    public async ValueTask<bool> FocusAsync(string path)
        => _focusTargets.TryGetValue(path, out var focus) && await focus();

    private sealed class FocusRegistration(BlazorFormState state, string path, Func<ValueTask<bool>> focus) : IDisposable
    {
        public void Dispose()
        {
            // Only remove our own registration: a field re-created at the same path during a re-render
            // registers before the old instance is disposed.
            if (state._focusTargets.TryGetValue(path, out var current) && ReferenceEquals(current, focus))
                state._focusTargets.Remove(path);
        }
    }

    /// <summary>Adds a message for a path (used by server-side validation results).</summary>
    public void SetServerError(string path, string message)
    {
        AddMessage(new BlazorFormValidationMessage(path, message));
        _touched.Add(path);
        NotifyChanged();
    }

    /// <summary>
    /// Replaces the form's messages with those returned by a server round-trip, so a failed POST can
    /// surface its errors on the right fields in one call.
    /// </summary>
    public void SetServerErrors(IEnumerable<BlazorFormValidationMessage> messages)
    {
        ReplaceAllMessages(messages.ToList());
        foreach (var m in _messages.Keys) _touched.Add(m);
        NotifyChanged();
    }

    public void ClearMessages()
    {
        _messages.Clear();
        NotifyChanged();
    }

    /// <summary>Clears the messages for a single field and everything nested beneath it.</summary>
    public void ClearMessages(string path)
    {
        RemoveMessagesUnder(path);
        NotifyChanged();
    }

    // ---------------------------------------------------------------- submission

    /// <summary>
    /// Runs the full submit cycle: marks every field touched, validates, and invokes
    /// <paramref name="onValid"/> or <paramref name="onInvalid"/>. Concurrent calls are ignored while a
    /// submit is in flight, which is what stops a double-click from posting twice.
    /// </summary>
    public async Task<bool> SubmitAsync(
        Func<BlazorFormState, Task>? onValid = null,
        Func<BlazorFormState, Task>? onInvalid = null)
    {
        if (IsSubmitting) return false;

        IsSubmitting = true;
        RegisterSubmitAttempt();
        MarkAllTouched();
        NotifyChanged();
        try
        {
            var valid = await ValidateAsync();
            if (valid)
            {
                if (onValid is not null) await onValid(this);
            }
            else if (onInvalid is not null)
            {
                await onInvalid(this);
            }
            return valid;
        }
        finally
        {
            IsSubmitting = false;
            NotifyChanged();
        }
    }

    // ---------------------------------------------------------------- reset

    /// <summary>
    /// Restores the values captured when the state was created, and clears validation, touched/dirty
    /// tracking, the submit count and the wizard position.
    /// </summary>
    /// <remarks>
    /// Values are restored, not deep-cloned: array elements that already existed when the state was
    /// created are put back by reference, so edits made *inside* one of those objects are not rolled
    /// back. Items added after construction are removed.
    /// </remarks>
    public void Reset()
    {
        foreach (var (path, value) in _initialValues)
        {
            if (value is List<object?> snapshot)
            {
                if (Data.GetValue(path) is IList live)
                {
                    live.Clear();
                    foreach (var item in snapshot) live.Add(item);
                }
                continue;
            }
            Data.SetValue(path, value);
        }

        _messages.Clear();
        _touched.Clear();
        _dirty.Clear();
        _conversionErrors.Clear();

        // Cancel before clearing: a load still in flight would otherwise land in the cache after the
        // reset and repopulate a select the user has just cleared.
        foreach (var path in _optionsCts.Keys.ToList()) CancelOptionsLoad(path);
        _loadedOptions.Clear();

        SubmitCount = 0;
        HasValidated = false;
        CurrentStepIndex = 0;
        ClampStep();
        NotifyChanged();
    }

    /// <summary>
    /// Re-captures the current values as the baseline for <see cref="Reset"/> and clears dirty
    /// tracking — call this after a successful save so the form is no longer reported as dirty.
    /// </summary>
    public void AcceptChanges()
    {
        _initialValues.Clear();
        CaptureInitialValues();
        _dirty.Clear();
        NotifyChanged();
    }

    private void CaptureInitialValues()
    {
        foreach (var (field, path) in EnumerateFieldPaths())
        {
            if (field.IsPresentational) continue;

            if (field.Type == BlazorFormFieldType.Array)
            {
                _initialValues[path] = Data.GetValue(path) is IList list ? list.Cast<object?>().ToList() : new List<object?>();
            }
            else if (field.Type != BlazorFormFieldType.Object)
            {
                _initialValues[path] = Data.GetValue(path);
            }
        }
    }

    // ---------------------------------------------------------------- wizard

    public BlazorFormStep? CurrentStep
    {
        get
        {
            if (!Definition.IsWizard) return null;
            ClampStep();
            return Definition.Steps[CurrentStepIndex];
        }
    }

    /// <summary>
    /// Moves off a step that a condition has just hidden. Without this the wizard would render — and
    /// validate — a step the schema says does not apply, with no way for the user to leave it.
    /// The nearest earlier visible step is preferred, so the user lands on ground they have already seen.
    /// </summary>
    private void ClampStep()
    {
        if (!Definition.IsWizard) return;
        if (CurrentStepIndex >= 0 && CurrentStepIndex < Definition.Steps.Count
            && IsStepVisible(Definition.Steps[CurrentStepIndex])) return;

        for (var i = Math.Min(CurrentStepIndex, Definition.Steps.Count) - 1; i >= 0; i--)
        {
            if (IsStepVisible(Definition.Steps[i])) { CurrentStepIndex = i; return; }
        }
        for (var i = Math.Max(CurrentStepIndex + 1, 0); i < Definition.Steps.Count; i++)
        {
            if (IsStepVisible(Definition.Steps[i])) { CurrentStepIndex = i; return; }
        }
        CurrentStepIndex = 0;
    }

    /// <summary>
    /// Drops messages belonging to a step the schema currently hides. A full validation walks every
    /// field, so without this a branch of the wizard the user was never shown could report errors that
    /// block submission and appear on no reachable page.
    /// </summary>
    private IReadOnlyList<BlazorFormValidationMessage> DropHiddenStepMessages(IReadOnlyList<BlazorFormValidationMessage> messages)
    {
        if (!Definition.IsWizard) return messages;

        var hidden = Definition.Steps.Where(s => !IsStepVisible(s)).SelectMany(s => s.Fields).ToList();
        if (hidden.Count == 0) return messages;

        // A field named by a visible step wins: two steps may legitimately share one field.
        var shown = Definition.Steps.Where(IsStepVisible).SelectMany(s => s.Fields).ToHashSet(StringComparer.OrdinalIgnoreCase);
        hidden.RemoveAll(shown.Contains);
        if (hidden.Count == 0) return messages;

        return messages.Where(m => !hidden.Any(f => BlazorFormPath.IsAtOrUnder(m.FieldPath, f))).ToList();
    }

    public bool IsFirstStep => !Definition.IsWizard || CurrentStepIndex <= FirstVisibleStepIndex();
    public bool IsLastStep => !Definition.IsWizard || CurrentStepIndex >= LastVisibleStepIndex();

    /// <summary>The steps that are currently visible, in schema order.</summary>
    public IReadOnlyList<BlazorFormStep> VisibleSteps => Definition.Steps.Where(IsStepVisible).ToList();

    /// <summary>
    /// The 1-based position of the current step among the visible ones — what a stepper should show,
    /// so numbering stays contiguous when a conditional step is skipped.
    /// </summary>
    public int CurrentStepNumber
    {
        get
        {
            var n = 0;
            for (var i = 0; i <= CurrentStepIndex && i < Definition.Steps.Count; i++)
                if (IsStepVisible(Definition.Steps[i])) n++;
            return n;
        }
    }

    /// <summary>Advances to the next visible step after validating the current one.</summary>
    public async ValueTask<bool> NextStepAsync()
    {
        if (!Definition.IsWizard) return false;
        if (!await ValidateStepAsync()) return false;

        for (var i = CurrentStepIndex + 1; i < Definition.Steps.Count; i++)
        {
            if (IsStepVisible(Definition.Steps[i]))
            {
                CurrentStepIndex = i;
                NotifyChanged();
                return true;
            }
        }
        return false;
    }

    /// <summary>Moves to the previous visible step (no validation).</summary>
    public void PreviousStep()
    {
        if (!Definition.IsWizard) return;
        for (var i = CurrentStepIndex - 1; i >= 0; i--)
        {
            if (IsStepVisible(Definition.Steps[i]))
            {
                CurrentStepIndex = i;
                NotifyChanged();
                return;
            }
        }
    }

    /// <summary>Jumps to a step by index. Hidden steps and out-of-range indices are ignored.</summary>
    public void GoToStep(int index)
    {
        if (!Definition.IsWizard || index < 0 || index >= Definition.Steps.Count) return;
        if (!IsStepVisible(Definition.Steps[index])) return;
        if (index == CurrentStepIndex) return;

        CurrentStepIndex = index;
        NotifyChanged();
    }

    /// <summary>Jumps to a step by its <see cref="BlazorFormStep.Id"/>.</summary>
    public void GoToStep(string id)
    {
        for (var i = 0; i < Definition.Steps.Count; i++)
        {
            if (string.Equals(Definition.Steps[i].Id, id, StringComparison.OrdinalIgnoreCase))
            {
                GoToStep(i);
                return;
            }
        }
    }

    // ---------------------------------------------------------------- arrays

    /// <summary>Appends a new item to an array field and returns its index.</summary>
    public int AddArrayItem(BlazorFormFieldDefinition arrayField, string arrayPath)
        => InsertArrayItem(arrayField, arrayPath, int.MaxValue);

    /// <summary>
    /// Inserts a new item at <paramref name="index"/> (clamped into range) and returns the index it
    /// landed at. Messages below the insertion point are re-indexed so they stay with their item.
    /// </summary>
    public int InsertArrayItem(BlazorFormFieldDefinition arrayField, string arrayPath, int index)
    {
        var list = EnsureList(arrayPath);
        var target = Math.Clamp(index, 0, list.Count);
        var item = CreateItem(arrayField, arrayPath);

        try
        {
            list.Insert(target, item);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return -1;
        }

        ShiftMessagesFrom(arrayPath, target, +1);
        ShiftPathSet(_touched, arrayPath, target, +1);
        ShiftPathSet(_dirty, arrayPath, target, +1);
        _dirty.Add(arrayPath);
        // The row was created a line ago, so every value in it is a placeholder the default may replace.
        ApplyItemDefaults(arrayField, BlazorFormPath.Combine(arrayPath, target), overwrite: true);
        RecomputeValues(arrayPath);
        RemoveMessagesFor(arrayPath); // the collection-size rule may now pass
        NotifyChanged();
        return target;
    }

    /// <summary>
    /// Inserts a copy of the item at <paramref name="index"/> directly after it and returns the new
    /// index, or -1 if the row could not be copied. This is the "add another one like that" a repeater
    /// of anything non-trivial always ends up needing.
    /// </summary>
    /// <remarks>
    /// Values are copied field by field through the schema rather than by cloning the object, so the
    /// new row is genuinely independent — a shallow copy would leave the two rows sharing the same
    /// nested objects and editing one would silently edit the other.
    /// </remarks>
    public int DuplicateArrayItem(BlazorFormFieldDefinition arrayField, string arrayPath, int index)
    {
        if (Data.GetValue(arrayPath) is not IList list || index < 0 || index >= list.Count) return -1;

        var source = BlazorFormPath.Combine(arrayPath, index);
        // Snapshot before inserting: the insert shifts the source row's own path.
        var values = ItemValuePaths(arrayField, source)
            .Select(p => (Relative: p[source.Length..], Value: Data.GetValue(p)))
            .ToList();

        var target = InsertArrayItem(arrayField, arrayPath, index + 1);
        if (target < 0) return -1;

        var targetPath = BlazorFormPath.Combine(arrayPath, target);
        foreach (var (relative, value) in values)
            Data.SetValue(targetPath + relative, value);

        _dirty.Add(arrayPath);
        RecomputeValues(arrayPath);
        NotifyChanged();
        return target;
    }

    /// <summary>Every value-bearing path inside one repeater row, in schema order.</summary>
    private IEnumerable<string> ItemValuePaths(BlazorFormFieldDefinition arrayField, string itemPath)
    {
        if (arrayField.ItemTemplate is not { } template) yield break;

        if (template.Type == BlazorFormFieldType.Object)
        {
            foreach (var child in template.Children)
                foreach (var path in Leaves(child, BlazorFormPath.Combine(itemPath, child.Name)))
                    yield return path;
        }
        else
        {
            foreach (var path in Leaves(template, itemPath))
                yield return path;
        }

        IEnumerable<string> Leaves(BlazorFormFieldDefinition field, string path)
        {
            if (field.Type == BlazorFormFieldType.Object)
            {
                foreach (var child in field.Children)
                    foreach (var p in Leaves(child, BlazorFormPath.Combine(path, child.Name)))
                        yield return p;
            }
            else if (field.Type == BlazorFormFieldType.Array && field.ItemTemplate is { } nested)
            {
                // A nested list is walked element by element rather than assigned across. Assigning it
                // would hand both rows the same list instance, and editing one would edit the other.
                var count = ArrayCount(path);
                for (var i = 0; i < count; i++)
                {
                    var nestedItem = BlazorFormPath.Combine(path, i);
                    if (nested.Type == BlazorFormFieldType.Object)
                    {
                        foreach (var child in nested.Children)
                            foreach (var p in Leaves(child, BlazorFormPath.Combine(nestedItem, child.Name)))
                                yield return p;
                    }
                    else
                    {
                        foreach (var p in Leaves(nested, nestedItem)) yield return p;
                    }
                }
            }
            else
            {
                yield return path;
            }
        }
    }

    /// <summary>Removes an item from an array field.</summary>
    public void RemoveArrayItem(string arrayPath, int index)
    {
        if (Data.GetValue(arrayPath) is not IList list || index < 0 || index >= list.Count) return;

        list.RemoveAt(index);

        var removedPath = BlazorFormPath.Combine(arrayPath, index);
        RemoveMessagesUnder(removedPath);
        RemovePathsUnder(_touched, removedPath);
        RemovePathsUnder(_dirty, removedPath);

        // Everything after the hole shifts up, so its errors and touched flags shift with it — otherwise
        // the item that moves into this slot inherits the removed item's state.
        ShiftMessagesFrom(arrayPath, index + 1, -1);
        ShiftPathSet(_touched, arrayPath, index + 1, -1);
        ShiftPathSet(_dirty, arrayPath, index + 1, -1);

        RemoveMessagesFor(arrayPath);
        _dirty.Add(arrayPath);
        RecomputeValues(arrayPath);
        NotifyChanged();
    }

    /// <summary>Moves an array item from one index to another.</summary>
    public void MoveArrayItem(string arrayPath, int from, int to)
    {
        if (Data.GetValue(arrayPath) is not IList list) return;
        if (from < 0 || from >= list.Count || to < 0 || to >= list.Count || from == to) return;

        var item = list[from];
        list.RemoveAt(from);
        list.Insert(to, item);

        // Indices shifted, so everything keyed by them is re-keyed to follow its item — the row the
        // user just moved keeps the error it already showed instead of appearing to be fixed.
        RemapArrayIndices(arrayPath, index => MapMovedIndex(index, from, to));
        _dirty.Add(arrayPath);
        RecomputeValues(arrayPath);
        NotifyChanged();
    }

    public int ArrayCount(string arrayPath) => Data.GetValue(arrayPath) switch
    {
        null => 0,
        ICollection c => c.Count,
        IEnumerable e and not string => e.Cast<object?>().Count(),
        _ => 0
    };

    /// <summary>Whether another item may be added, given <see cref="BlazorFormFieldDefinition.MaxItems"/>.</summary>
    public bool CanAddArrayItem(BlazorFormFieldDefinition arrayField, string arrayPath)
        => arrayField.MaxItems is not { } max || ArrayCount(arrayPath) < max;

    /// <summary>Whether an item may be removed, given <see cref="BlazorFormFieldDefinition.MinItems"/>.</summary>
    public bool CanRemoveArrayItem(BlazorFormFieldDefinition arrayField, string arrayPath)
        => arrayField.MinItems is not { } min || ArrayCount(arrayPath) > min;

    // ---------------------------------------------------------------- internals

    private int FirstVisibleStepIndex()
    {
        for (var i = 0; i < Definition.Steps.Count; i++)
            if (IsStepVisible(Definition.Steps[i])) return i;
        return 0;
    }

    private int LastVisibleStepIndex()
    {
        for (var i = Definition.Steps.Count - 1; i >= 0; i--)
            if (IsStepVisible(Definition.Steps[i])) return i;
        return 0;
    }

    private void NotifyChanged()
    {
        if (_disposed) return;

        // Inside a batch the notification is deferred, not dropped: the outermost Batch raises exactly
        // one at the end, however many writes it contained.
        if (_batchDepth > 0)
        {
            _batchChanged = true;
            return;
        }

        StateChanged?.Invoke();
    }

    private async ValueTask<IReadOnlyList<BlazorFormValidationMessage>> MergeExternal(IReadOnlyList<BlazorFormValidationMessage> messages)
    {
        if (ExternalValidator is null) return messages;
        var external = await ExternalValidator(Definition, Data, Services);
        return messages.Concat(external.Where(m => !IsHidden(m.FieldPath))).ToList();
    }

    /// <summary>
    /// Whether a path lies under a field the schema is currently hiding. An external validator — a
    /// FluentValidation validator, say — sees the whole model and knows nothing about the form's
    /// conditions, so a rule on a branch the user was never shown would otherwise block submission with
    /// an error on a control that is not on the page.
    /// </summary>
    private bool IsHidden(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        foreach (var (field, fieldPath) in EnumerateFieldPaths())
        {
            if (field.VisibleWhen is null) continue;
            if (!BlazorFormPath.IsAtOrUnder(path, fieldPath)) continue;
            if (!field.VisibleWhen.Evaluate(ScopeFor(fieldPath))) return true;
        }
        return false;
    }

    private void ReplaceAllMessages(IReadOnlyList<BlazorFormValidationMessage> messages)
    {
        _messages.Clear();
        foreach (var m in messages) AddMessage(m);
    }

    private void AddMessage(BlazorFormValidationMessage message)
    {
        if (!_messages.TryGetValue(message.FieldPath, out var list))
            _messages[message.FieldPath] = list = new List<BlazorFormValidationMessage>();
        list.Add(message);
    }

    private void RemoveMessagesFor(string path) => _messages.Remove(path);

    private void RemoveMessagesUnder(string path)
    {
        var keys = _messages.Keys.Where(k => BlazorFormPath.IsAtOrUnder(k, path)).ToList();
        foreach (var k in keys) _messages.Remove(k);
    }

    /// <summary>
    /// Re-keys messages belonging to items at or after <paramref name="fromIndex"/> by
    /// <paramref name="delta"/>, keeping errors attached to the item that owns them after an insert or
    /// remove.
    /// </summary>
    private void ShiftMessagesFrom(string arrayPath, int fromIndex, int delta)
    {
        var affected = _messages.Keys
            .Select(k => (Key: k, Index: BlazorFormPath.IndexIn(k, arrayPath)))
            .Where(x => x.Index is { } i && i >= fromIndex)
            .OrderBy(x => delta > 0 ? -x.Index!.Value : x.Index!.Value)
            .ToList();

        foreach (var (key, index) in affected)
        {
            if (BlazorFormPath.Reindex(key, arrayPath, index!.Value + delta) is not { } newKey) continue;
            if (!_messages.Remove(key, out var list)) continue;

            var rebased = list.Select(m => m with { FieldPath = BlazorFormPath.Reindex(m.FieldPath, arrayPath, index.Value + delta) ?? m.FieldPath }).ToList();
            if (_messages.TryGetValue(newKey, out var existing)) existing.AddRange(rebased);
            else _messages[newKey] = rebased;
        }
    }

    /// <summary>
    /// Where an item ends up after a move. Everything between the old and the new position shifts one
    /// place to make room, which is what keeps the rest of the rows lined up with their own state.
    /// </summary>
    private static int MapMovedIndex(int index, int from, int to)
    {
        if (index == from) return to;
        if (from < to) return index > from && index <= to ? index - 1 : index;
        return index >= to && index < from ? index + 1 : index;
    }

    /// <summary>
    /// Re-keys every message, touched and dirty entry belonging to an element of
    /// <paramref name="arrayPath"/> through <paramref name="map"/>. Entries are lifted out before any
    /// are put back, so two rows swapping places cannot collide mid-flight.
    /// </summary>
    private void RemapArrayIndices(string arrayPath, Func<int, int> map)
    {
        RemapMessages(arrayPath, map);
        RemapPathSet(_touched, arrayPath, map);
        RemapPathSet(_dirty, arrayPath, map);
    }

    private void RemapMessages(string arrayPath, Func<int, int> map)
    {
        var lifted = new List<(string Key, List<BlazorFormValidationMessage> Messages)>();
        foreach (var key in _messages.Keys.ToList())
        {
            if (BlazorFormPath.IndexIn(key, arrayPath) is not { } index) continue;
            var newKey = BlazorFormPath.Reindex(key, arrayPath, map(index));
            if (newKey is null) continue;

            _messages.Remove(key, out var list);
            lifted.Add((newKey, list!.Select(m => m with
            {
                FieldPath = BlazorFormPath.Reindex(m.FieldPath, arrayPath, map(index)) ?? m.FieldPath
            }).ToList()));
        }

        foreach (var (key, messages) in lifted)
        {
            if (_messages.TryGetValue(key, out var existing)) existing.AddRange(messages);
            else _messages[key] = messages;
        }
    }

    private static void RemapPathSet(HashSet<string> set, string arrayPath, Func<int, int> map)
    {
        if (set.Count == 0) return;

        var lifted = new List<string>();
        foreach (var key in set.ToList())
        {
            if (BlazorFormPath.IndexIn(key, arrayPath) is not { } index) continue;
            set.Remove(key);
            if (BlazorFormPath.Reindex(key, arrayPath, map(index)) is { } newKey) lifted.Add(newKey);
        }
        foreach (var key in lifted) set.Add(key);
    }

    /// <summary>Drops every entry in a path set at or beneath <paramref name="path"/>.</summary>
    private static void RemovePathsUnder(HashSet<string> set, string path)
    {
        if (set.Count == 0) return;
        foreach (var key in set.Where(k => BlazorFormPath.IsAtOrUnder(k, path)).ToList())
            set.Remove(key);
    }

    /// <summary>
    /// Re-keys the entries of a path set belonging to array items at or after
    /// <paramref name="fromIndex"/> by <paramref name="delta"/>, mirroring
    /// <see cref="ShiftMessagesFrom"/>.
    /// </summary>
    private static void ShiftPathSet(HashSet<string> set, string arrayPath, int fromIndex, int delta)
    {
        if (set.Count == 0) return;

        var affected = set
            .Select(k => (Key: k, Index: BlazorFormPath.IndexIn(k, arrayPath)))
            .Where(x => x.Index is { } i && i >= fromIndex)
            .OrderBy(x => delta > 0 ? -x.Index!.Value : x.Index!.Value)
            .ToList();

        foreach (var (key, index) in affected)
        {
            set.Remove(key);
            if (BlazorFormPath.Reindex(key, arrayPath, index!.Value + delta) is { } newKey)
                set.Add(newKey);
        }
    }

    private void ApplyDefaults()
    {
        foreach (var field in Definition.Fields)
            ApplyDefault(field, field.Name);
    }

    /// <summary>
    /// Creates the rows a repeater marked <c>SeedMinItems()</c> is required to have, so a form whose
    /// schema says "at least one line" opens with a line rather than with an error about not having one.
    /// </summary>
    /// <remarks>
    /// Opt-in, not automatic: on an edit form loaded from storage, "this record has no lines" can be
    /// the truth, and inventing one would be the library editing the user's data on their behalf.
    /// </remarks>
    private void SeedRequiredArrayItems()
    {
        foreach (var (field, path) in EnumerateFieldPaths().ToList())
        {
            if (field.Type != BlazorFormFieldType.Array || field.MinItems is not { } min || min <= 0) continue;
            if (!field.Attributes.TryGetValue("seedMinItems", out var seed) || seed is not true) continue;

            for (var i = ArrayCount(path); i < min; i++)
                if (InsertArrayItem(field, path, int.MaxValue) < 0) break;
        }
    }

    /// <summary>
    /// Applies a field's declared default. <paramref name="overwrite"/> distinguishes the two cases
    /// that look identical from here: data the form was handed, where only a missing value may be
    /// filled in, and a row the form has just created, where every value is a placeholder. Without the
    /// distinction a default of 1 could never reach an <c>int</c> — a fresh one already reads as 0, and
    /// 0 is indistinguishable from a deliberate answer.
    /// </summary>
    private void ApplyDefault(BlazorFormFieldDefinition field, string path, bool overwrite = false)
    {
        // A heading has a name so the schema can address it, not because the model has a property of
        // that name. Writing to it would fail silently on a typed model and litter a dictionary one.
        if (field.IsPresentational) return;

        if (field.DefaultValue is not null && (overwrite || Data.GetValue(path) is null))
            Data.SetValue(path, field.DefaultValue);

        if (field.Type == BlazorFormFieldType.Object)
        {
            foreach (var child in field.Children)
                ApplyDefault(child, BlazorFormPath.Combine(path, child.Name), overwrite);
        }
        else if (field.Type == BlazorFormFieldType.Array && field.ItemTemplate is not null && !overwrite)
        {
            // Existing rows are completed, never rewritten: a model loaded from storage keeps its data.
            var count = ArrayCount(path);
            for (var i = 0; i < count; i++)
                ApplyItemDefaults(field, BlazorFormPath.Combine(path, i), overwrite: false);
        }
    }

    /// <summary>
    /// Seeds a repeater row from its template's default values, so "quantity starts at 1" holds for
    /// every row the user adds and not only for the ones the form was created with.
    /// </summary>
    private void ApplyItemDefaults(BlazorFormFieldDefinition arrayField, string itemPath, bool overwrite)
    {
        if (arrayField.ItemTemplate is not { } template) return;

        if (template.Type == BlazorFormFieldType.Object)
        {
            foreach (var child in template.Children)
                ApplyDefault(child, BlazorFormPath.Combine(itemPath, child.Name), overwrite);
        }
        else
        {
            ApplyDefault(template, itemPath, overwrite);
        }
    }

    /// <summary>
    /// Walks the schema yielding every field paired with its absolute path, descending into objects and
    /// into the live elements of arrays.
    /// </summary>
    private IEnumerable<(BlazorFormFieldDefinition Field, string Path)> EnumerateFieldPaths()
    {
        foreach (var field in Definition.Fields)
            foreach (var pair in Descend(field, field.Name))
                yield return pair;

        IEnumerable<(BlazorFormFieldDefinition, string)> Descend(BlazorFormFieldDefinition field, string path)
        {
            yield return (field, path);

            if (field.Type == BlazorFormFieldType.Object)
            {
                foreach (var child in field.Children)
                    foreach (var pair in Descend(child, BlazorFormPath.Combine(path, child.Name)))
                        yield return pair;
            }
            else if (field.Type == BlazorFormFieldType.Array && field.ItemTemplate is { } template)
            {
                var count = ArrayCount(path);
                for (var i = 0; i < count; i++)
                {
                    var itemPath = BlazorFormPath.Combine(path, i);
                    if (template.Type == BlazorFormFieldType.Object)
                    {
                        foreach (var child in template.Children)
                            foreach (var pair in Descend(child, BlazorFormPath.Combine(itemPath, child.Name)))
                                yield return pair;
                    }
                    else
                    {
                        foreach (var pair in Descend(template, itemPath))
                            yield return pair;
                    }
                }
            }
        }
    }

    private IEnumerable<string> EnumerateValuePaths()
        => EnumerateFieldPaths()
            .Where(p => p.Field.Type != BlazorFormFieldType.Object && !p.Field.IsPresentational)
            .Select(p => p.Path);

    private IList EnsureList(string arrayPath)
    {
        if (Data.GetValue(arrayPath) is IList existing) return existing;

        IList newList;
        if (Data is BlazorFormDictionaryDataAccessor)
        {
            newList = new List<object?>();
        }
        else
        {
            var elementType = Data.GetElementType(arrayPath) ?? typeof(object);
            newList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        }
        Data.SetValue(arrayPath, newList);
        // Re-read in case the accessor wrapped/converted the value.
        return Data.GetValue(arrayPath) as IList ?? newList;
    }

    private object? CreateItem(BlazorFormFieldDefinition arrayField, string arrayPath)
    {
        var template = arrayField.ItemTemplate;
        var elementType = Data.GetElementType(arrayPath);

        if (Data is BlazorFormDictionaryDataAccessor || elementType is null || elementType == typeof(object))
            return template?.Type == BlazorFormFieldType.Object ? new Dictionary<string, object?>() : null;

        if (elementType == typeof(string)) return null;
        var underlying = Nullable.GetUnderlyingType(elementType) ?? elementType;
        try { return underlying.IsValueType ? Activator.CreateInstance(underlying) : Activator.CreateInstance(elementType); }
        catch (Exception ex) when (ex is MissingMethodException or MemberAccessException or TargetInvocationException) { return null; }
    }

    /// <summary>Cancels any in-flight validation and detaches listeners.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _formValidationCts?.Cancel();
        _formValidationCts?.Dispose();
        _formValidationCts = null;

        foreach (var cts in _fieldValidationCts.Values.Concat(_optionsCts.Values))
        {
            cts.Cancel();
            cts.Dispose();
        }
        _fieldValidationCts.Clear();
        _optionsCts.Clear();
        _focusTargets.Clear();

        StateChanged = null;
        FieldChanged = null;
    }
}
