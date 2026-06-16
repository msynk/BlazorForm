using System.Collections;

namespace BlazorForm;

/// <summary>
/// Runtime state for a form instance: holds the data, validation results, touched/dirty tracking,
/// wizard position and submission state. The UI layer binds to this and reacts to <see cref="StateChanged"/>.
/// </summary>
public sealed class BlazorFormState
{
    private readonly BlazorFormValidator _validator = new();
    private readonly Dictionary<string, List<BlazorFormValidationMessage>> _messages = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _touched = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _dirty = new(StringComparer.OrdinalIgnoreCase);

    public BlazorFormState(BlazorFormDefinition definition, IBlazorFormDataAccessor data, IServiceProvider? services = null)
    {
        Definition = definition;
        Data = data;
        Services = services;
        ApplyDefaults();
    }

    /// <summary>The schema being rendered.</summary>
    public BlazorFormDefinition Definition { get; }

    /// <summary>The data store (typed model or dictionary).</summary>
    public IBlazorFormDataAccessor Data { get; }

    /// <summary>Optional service provider for validators that need DI.</summary>
    public IServiceProvider? Services { get; }

    /// <summary>Optional external validator merged with built-in rules (set by integrations).</summary>
    public BlazorFormExternalValidator? ExternalValidator { get; set; }

    /// <summary>Index of the active wizard step (ignored for non-wizard forms).</summary>
    public int CurrentStepIndex { get; private set; }

    /// <summary>Number of times submission has been attempted.</summary>
    public int SubmitCount { get; private set; }

    /// <summary>True while an async submit/validation is in flight.</summary>
    public bool IsValidating { get; private set; }

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
        try { return (T)Convert.ChangeType(v, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T)); }
        catch { return default; }
    }

    /// <summary>Sets a value, marks the field dirty and notifies listeners.</summary>
    public void SetValue(string path, object? value)
    {
        Data.SetValue(path, value);
        _dirty.Add(path);
        _touched.Add(path);
        FieldChanged?.Invoke(path);
        NotifyChanged();
    }

    public bool IsTouched(string path) => _touched.Contains(path);
    public bool IsDirty(string path) => _dirty.Contains(path);
    public bool IsFormDirty => _dirty.Count > 0;

    public void MarkTouched(string path)
    {
        if (_touched.Add(path)) NotifyChanged();
    }

    // ---------------------------------------------------------------- conditional state

    /// <summary>Whether a field is currently visible given the data.</summary>
    public bool IsVisible(BlazorFormFieldDefinition field)
        => field.VisibleWhen is null || field.VisibleWhen.Evaluate(Data);

    /// <summary>Whether a field is currently disabled given the data.</summary>
    public bool IsDisabled(BlazorFormFieldDefinition field)
        => field.ReadOnly || (field.DisabledWhen is not null && field.DisabledWhen.Evaluate(Data));

    /// <summary>Whether a wizard step is currently visible.</summary>
    public bool IsStepVisible(BlazorFormStep step)
        => step.VisibleWhen is null || step.VisibleWhen.Evaluate(Data);

    // ---------------------------------------------------------------- validation

    /// <summary>Records a submit attempt (so validation messages become visible) and returns the count.</summary>
    public int RegisterSubmitAttempt()
    {
        SubmitCount++;
        return SubmitCount;
    }

    /// <summary>All current validation messages across the form.</summary>
    public IEnumerable<BlazorFormValidationMessage> AllMessages => _messages.Values.SelectMany(x => x);

    /// <summary>Messages for a specific field path.</summary>
    public IReadOnlyList<BlazorFormValidationMessage> MessagesFor(string path)
        => _messages.TryGetValue(path, out var list) ? list : Array.Empty<BlazorFormValidationMessage>();

    /// <summary>True if there is at least one error-severity message.</summary>
    public bool HasErrors => AllMessages.Any(m => m.Severity == BlazorFormValidationSeverity.Error);

    /// <summary>Validates the entire form and stores the results.</summary>
    public async ValueTask<bool> ValidateAsync(bool includeAsync = true)
    {        IsValidating = true;
        NotifyChanged();
        try
        {
            var messages = await _validator.ValidateAsync(Definition, Data, Services, includeAsync: includeAsync);
            var merged = await MergeExternal(messages);
            ReplaceAllMessages(merged);
            return !HasErrors;
        }
        finally
        {
            IsValidating = false;
            NotifyChanged();
        }
    }

    /// <summary>Validates the fields of the current wizard step.</summary>
    public async ValueTask<bool> ValidateStepAsync(bool includeAsync = true)
    {
        if (!Definition.IsWizard) return await ValidateAsync(includeAsync);

        var step = Definition.Steps[CurrentStepIndex];
        var names = new HashSet<string>(step.Fields, StringComparer.OrdinalIgnoreCase);
        var messages = await _validator.ValidateAsync(Definition, Data, Services, names, includeAsync);
        // Replace only messages for fields in this step.
        foreach (var name in step.Fields)
            RemoveMessagesUnder(name);
        foreach (var m in messages)
            AddMessage(m);
        NotifyChanged();
        return !messages.Any(m => m.Severity == BlazorFormValidationSeverity.Error);
    }

    /// <summary>Validates a single field and refreshes only its messages.</summary>
    public async ValueTask ValidateFieldAsync(BlazorFormFieldDefinition field, string path, bool includeAsync = true)
    {
        var messages = await _validator.ValidateFieldAsync(field, path, Data, Services, includeAsync);
        RemoveMessagesUnder(path);
        foreach (var m in messages) AddMessage(m);
        NotifyChanged();
    }

    /// <summary>Replaces all messages for a path (used by external/server validation).</summary>
    public void SetServerError(string path, string message)
    {
        AddMessage(new BlazorFormValidationMessage(path, message));
        _touched.Add(path);
        NotifyChanged();
    }

    public void ClearMessages()
    {
        _messages.Clear();
        NotifyChanged();
    }

    // ---------------------------------------------------------------- wizard

    public BlazorFormStep? CurrentStep => Definition.IsWizard ? Definition.Steps[CurrentStepIndex] : null;

    public bool IsFirstStep => CurrentStepIndex == 0;
    public bool IsLastStep => !Definition.IsWizard || CurrentStepIndex >= LastVisibleStepIndex();

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

    public void GoToStep(int index)
    {
        if (Definition.IsWizard && index >= 0 && index < Definition.Steps.Count)
        {
            CurrentStepIndex = index;
            NotifyChanged();
        }
    }

    // ---------------------------------------------------------------- arrays

    /// <summary>Appends a new item to an array field and returns its index.</summary>
    public int AddArrayItem(BlazorFormFieldDefinition arrayField, string arrayPath)
    {
        var list = EnsureList(arrayPath);
        list.Add(CreateItem(arrayField, arrayPath));
        _dirty.Add(arrayPath);
        NotifyChanged();
        return list.Count - 1;
    }

    /// <summary>Removes an item from an array field.</summary>
    public void RemoveArrayItem(string arrayPath, int index)
    {
        if (Data.GetValue(arrayPath) is IList list && index >= 0 && index < list.Count)
        {
            list.RemoveAt(index);
            _dirty.Add(arrayPath);
            RemoveMessagesUnder(arrayPath); // indices shifted; clear and revalidate later
            NotifyChanged();
        }
    }

    /// <summary>Moves an array item from one index to another.</summary>
    public void MoveArrayItem(string arrayPath, int from, int to)
    {
        if (Data.GetValue(arrayPath) is IList list &&
            from >= 0 && from < list.Count && to >= 0 && to < list.Count)
        {
            var item = list[from];
            list.RemoveAt(from);
            list.Insert(to, item);
            _dirty.Add(arrayPath);
            NotifyChanged();
        }
    }

    public int ArrayCount(string arrayPath)
        => Data.GetValue(arrayPath) is IEnumerable e and not string ? e.Cast<object?>().Count() : 0;

    // ---------------------------------------------------------------- internals

    private int LastVisibleStepIndex()
    {
        for (var i = Definition.Steps.Count - 1; i >= 0; i--)
            if (IsStepVisible(Definition.Steps[i])) return i;
        return 0;
    }

    private void NotifyChanged() => StateChanged?.Invoke();

    private async ValueTask<IReadOnlyList<BlazorFormValidationMessage>> MergeExternal(IReadOnlyList<BlazorFormValidationMessage> messages)
    {
        if (ExternalValidator is null) return messages;
        var external = await ExternalValidator(Definition, Data, Services);
        return messages.Concat(external).ToList();
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

    private void RemoveMessagesUnder(string path)
    {
        var keys = _messages.Keys
            .Where(k => k.Equals(path, StringComparison.OrdinalIgnoreCase) ||
                        k.StartsWith(path + ".", StringComparison.OrdinalIgnoreCase) ||
                        k.StartsWith(path + "[", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var k in keys) _messages.Remove(k);
    }

    private void ApplyDefaults()
    {
        foreach (var field in Definition.Fields)
            ApplyDefault(field, field.Name);
    }

    private void ApplyDefault(BlazorFormFieldDefinition field, string path)
    {
        if (field.DefaultValue is not null && Data.GetValue(path) is null)
            Data.SetValue(path, field.DefaultValue);

        if (field.Type == BlazorFormFieldType.Object)
            foreach (var child in field.Children)
                ApplyDefault(child, BlazorFormPath.Combine(path, child.Name));
    }

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
            var listType = typeof(List<>).MakeGenericType(elementType);
            newList = (IList)Activator.CreateInstance(listType)!;
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
        catch { return null; }
    }
}
