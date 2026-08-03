using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorForm;

/// <summary>Base class shared by all built-in field input components.</summary>
public abstract class BlazorFormInputBase : ComponentBase, IDisposable
{
    private CancellationTokenSource? _debounce;
    private bool _focusRequested;
    private bool _autofocusApplied;
    private string? _pendingRaw;
    private bool _hasPendingRaw;
    private int? _liveLength;
    private string? _lastCommitted;

    /// <summary>The render context for the field being displayed.</summary>
    [Parameter, EditorRequired] public BlazorFormFieldContext Context { get; set; } = default!;

    protected BlazorFormFieldDefinition Field => Context.Field;

    /// <summary>
    /// The element the control renders. Renderers bind it with <c>@ref</c> so
    /// <see cref="BlazorFormFieldDefinition.Autofocus"/> and focus-the-first-error can actually move
    /// the caret there.
    /// </summary>
    protected ElementReference Element { get; set; }

    /// <summary>True when a condition has disabled the field. Read-only is rendered separately — see <see cref="ReadOnly"/>.</summary>
    protected bool Disabled => Context.IsDisabled;

    /// <summary>True when the field is read-only.</summary>
    protected bool ReadOnly => Context.IsReadOnly;

    /// <summary>
    /// For controls that have no <c>readonly</c> attribute of their own (select, checkbox, radio),
    /// read-only has to be expressed as <c>disabled</c> — there is no other way to stop the browser
    /// changing them.
    /// </summary>
    protected bool DisabledOrReadOnly => Disabled || ReadOnly;

    protected string CssClass => Context.HasError && Context.ShowMessages
        ? "ff-input ff-input--invalid"
        : "ff-input";

    /// <summary>Value for <c>aria-invalid</c>, or null to omit the attribute.</summary>
    protected string? AriaInvalid => Context.AriaInvalid;

    /// <summary>Ids of the help text and error list, for <c>aria-describedby</c>.</summary>
    protected string? DescribedBy => Context.DescribedBy;

    /// <summary>Value for <c>aria-required</c>, or null to omit the attribute.</summary>
    protected string? AriaRequired => Context.IsRequired ? "true" : null;

    /// <summary>
    /// The accessible name to use when the visible label is suppressed. Null the rest of the time,
    /// because a real <c>&lt;label&gt;</c> already points at the control and an <c>aria-label</c> would
    /// silently override it.
    /// </summary>
    protected string? AriaLabel => Field.ShowLabel ? null : Field.Label ?? Field.Name;

    /// <summary>
    /// Extra attributes declared on the schema, splatted onto the control. Attributes the renderer sets
    /// itself win, so nothing here can break the id/aria wiring.
    /// </summary>
    protected IReadOnlyDictionary<string, object>? ExtraAttributes => Context.InputAttributes;

    /// <summary>Lower bound, formatted invariantly so it is never localised into invalid HTML.</summary>
    protected string? MinAttribute => BlazorFormNumber.ToAttribute(Field.Min);

    /// <summary>Upper bound, formatted invariantly.</summary>
    protected string? MaxAttribute => BlazorFormNumber.ToAttribute(Field.Max);

    /// <summary>
    /// Whether this field writes back on every keystroke rather than on the browser's <c>change</c>
    /// event. Both handlers are always wired; each is a no-op when it is not the configured one, which
    /// keeps the markup of every renderer identical.
    /// </summary>
    protected bool UpdatesOnInput => Field.UpdateOn == BlazorFormUpdateTrigger.Input;

    /// <summary>Handles the browser's <c>change</c> event, ignoring it for input-driven fields.</summary>
    protected Task OnChange(ChangeEventArgs e)
        => UpdatesOnInput ? Task.CompletedTask : Context.SetFromStringAsync(e.Value?.ToString());

    /// <summary>
    /// Handles the browser's <c>input</c> event. It is only wired at all when something needs it (see
    /// <see cref="InputHandler"/>), so a change-driven field costs no round-trip per keystroke; when it
    /// is wired only for the character counter, the keystroke updates the counter and nothing else.
    /// </summary>
    protected Task OnInput(ChangeEventArgs e)
    {
        var raw = e.Value?.ToString();
        _liveLength = raw?.Length ?? 0;
        return UpdatesOnInput ? Debounced(raw) : Task.CompletedTask;
    }

    /// <summary>
    /// The <c>input</c> handler to bind, or a callback with no delegate — which Blazor omits from the
    /// rendered element entirely. That omission is the point: an <c>oninput</c> attribute costs a
    /// server round-trip on every keystroke of a Blazor Server circuit, and most fields write on
    /// <c>change</c> and would do nothing with it.
    /// </summary>
    protected EventCallback<ChangeEventArgs> InputHandler
        => WantsInputEvent ? EventCallback.Factory.Create<ChangeEventArgs>(this, OnInput) : default;

    /// <summary>
    /// Whether this control has any use for the <c>input</c> event: it writes as the user types, or it
    /// shows a live character counter.
    /// </summary>
    protected virtual bool WantsInputEvent => UpdatesOnInput || Field.ShowCharacterCount;

    /// <summary>
    /// The length to show in a character counter: what the user has typed so far, not what has been
    /// committed to the model. Under the default <see cref="BlazorFormUpdateTrigger.Change"/> those are
    /// the same thing only on blur, which is precisely when a counter is no longer useful.
    /// </summary>
    protected int DisplayLength => _liveLength ?? Context.StringValue.Length;

    /// <summary>
    /// The value for the HTML <c>maxlength</c> attribute, or null when the limit should be shown rather
    /// than enforced.
    /// </summary>
    /// <remarks>
    /// A field with a live counter deliberately drops it. <c>maxlength</c> is destructive: pasting a
    /// slightly-too-long answer silently truncates it, with no message and nothing to undo. A counter
    /// plus the length rule is the non-destructive equivalent — the user keeps their text, sees the
    /// count go red, and edits it down. This is why GOV.UK's character count removes the attribute too.
    /// </remarks>
    protected int? MaxLengthAttribute => Field.ShowCharacterCount ? null : Field.MaxLength;

    /// <summary>
    /// What a polite live region should say about the character limit, or null when it should stay
    /// silent. The visible counter is <c>aria-hidden</c> — a number re-announced on every keystroke
    /// would bury everything else — but leaving assistive technology with nothing at all means a
    /// screen-reader user meets the limit by having their typing stop working. So the count is
    /// announced only as the limit comes into view, and again once it is passed.
    /// </summary>
    /// <remarks>
    /// The threshold defaults to a tenth of the limit (at least five characters) and can be set per
    /// field with <c>Attr("countAnnounceAt", 25)</c>.
    /// </remarks>
    protected string? CharacterCountAnnouncement
    {
        get
        {
            if (!Field.ShowCharacterCount || Field.MaxLength is not { } max || max <= 0) return null;

            var remaining = max - DisplayLength;
            if (remaining < 0) return Context.Text(BlazorFormMessageKeys.CharactersOver, -remaining);

            var threshold = Math.Max(1, Attr("countAnnounceAt", Math.Max(5, max / 10)));
            return remaining <= threshold ? Context.Text(BlazorFormMessageKeys.CharactersRemaining, remaining) : null;
        }
    }

    /// <summary>
    /// Writes the value once the user pauses. A newer keystroke supersedes the pending write, so a
    /// debounced field performs one write per pause instead of one per character — the difference
    /// between a remote uniqueness check that is usable and one that is not.
    /// </summary>
    private async Task Debounced(string? raw)
    {
        var delay = Field.DebounceMilliseconds;
        if (delay <= 0)
        {
            await Context.SetFromStringAsync(raw);
            return;
        }

        CancelDebounce();
        _pendingRaw = raw;
        _hasPendingRaw = true;

        var cts = _debounce = new CancellationTokenSource();
        try
        {
            await Task.Delay(delay, cts.Token);
            _hasPendingRaw = false;
            // Async rules run here and only here on the typing path. A pause is what a debounce exists
            // to detect, and it is the moment a remote uniqueness check stops being one request per
            // character and becomes one request per thing the user actually typed — which is the whole
            // reason the field asked to be debounced. An undebounced field still waits for blur.
            await Context.SetFromStringAsync(raw, includeAsync: true);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a later keystroke, or the component went away. The value is still held in
            // _pendingRaw, so a blur arriving before the delay elapses can still write it.
        }
    }

    protected async Task OnBlur()
    {
        // A pending debounced write has to *land* before the field is judged. Dropping it would judge
        // the value as it stood one keystroke ago — and, worse, leave the user's last characters in the
        // box but never in the model.
        await FlushAsync();
        await Context.BlurAsync();
    }

    /// <summary>
    /// Writes a debounced value that is still waiting out its delay, immediately. Called on blur, so
    /// leaving a field never discards what was typed into it.
    /// </summary>
    protected async Task FlushAsync()
    {
        if (!_hasPendingRaw) return;

        var raw = _pendingRaw;
        _hasPendingRaw = false;
        _pendingRaw = null;
        CancelDebounce();
        await Context.SetFromStringAsync(raw);
    }

    protected override void OnParametersSet()
    {
        // Registered per path so the form can send focus here — "jump to the first error" and the error
        // summary's links both end up going through this.
        if (_focusRegistration is null || _registeredPath != Context.Path)
        {
            _focusRegistration?.Dispose();
            _registeredPath = Context.Path;
            _focusRegistration = Context.State.RegisterFocusTarget(_registeredPath, FocusAsync);
        }

        // A value that changed underneath us — a reset, a computed update, another field's cascade —
        // supersedes whatever the user had half-typed, so the counter goes back to tracking the model.
        var committed = Context.StringValue;
        if (!string.Equals(_lastCommitted, committed, StringComparison.Ordinal))
        {
            _lastCommitted = committed;
            _liveLength = null;
        }

        if (_autofocusApplied || !Field.Autofocus || DisabledOrReadOnly) return;
        _autofocusApplied = true;
        _focusRequested = true;
    }

    private IDisposable? _focusRegistration;
    private string? _registeredPath;

    /// <summary>
    /// Applies <see cref="BlazorFormFieldDefinition.Autofocus"/> for real. The HTML attribute of the
    /// same name is only honoured on the browser's initial page load, which an interactively rendered
    /// Blazor form almost never is.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_focusRequested) return;
        _focusRequested = false;
        await FocusAsync();
    }

    /// <summary>
    /// Moves focus to the control. Returns false when there is nothing to focus — a static prerender
    /// with no circuit, or a renderer that never bound <see cref="Element"/> — so a caller walking a
    /// list of candidates can try the next one instead of believing it has succeeded.
    /// </summary>
    public virtual async ValueTask<bool> FocusAsync()
    {
        try
        {
            await Element.FocusAsync();
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or JSDisconnectedException or ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>Reads a renderer hint from <see cref="BlazorFormFieldDefinition.Attributes"/>.</summary>
    protected T? Attr<T>(string key, T? fallback = default)
    {
        if (!Field.Attributes.TryGetValue(key, out var raw) || raw is null) return fallback;
        if (raw is T typed) return typed;
        return BlazorFormValueConverter.TryCoerce(raw, typeof(T), out var converted) && converted is T c ? c : fallback;
    }

    private void CancelDebounce()
    {
        _debounce?.Cancel();
        _debounce?.Dispose();
        _debounce = null;
    }

    public virtual void Dispose()
    {
        CancelDebounce();
        _focusRegistration?.Dispose();
        _focusRegistration = null;
        GC.SuppressFinalize(this);
    }
}
