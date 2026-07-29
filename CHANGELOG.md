# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.3.0] — Unreleased

### Added

- **Live updates and debouncing** — `UpdateOnInput(debounceMilliseconds)` writes the value as the user
  types instead of on blur. The `input` handler is only wired when a field actually needs it, so a
  change-driven field still costs no round-trip per keystroke on a Blazor Server circuit.
- **Field polish** — `Prefix`/`Suffix` affixes, a live `CharacterCount()`, `HideLabel()` (the label
  becomes the control's accessible name), `Revealable()` on a password, `Clearable()`, `AsSwitch()`,
  and `Suggest(...)` for `<datalist>` hints that propose without restricting.
- **Static content** — `BlazorFormFieldType.Static` and `Static(name, heading, text)` put section
  headings, explanatory paragraphs and dividers in the schema, so a long form does not have to
  interleave page markup with generated fields.
- **`InputAttr(name, value)`** — arbitrary HTML attributes splatted onto the rendered control. The
  renderer's own id and ARIA attributes always win.
- **Repeater actions** — `DuplicateArrayItem(...)` (and an opt-in per-row button) copies a row field by
  field, so the copy shares no nested object with the original. `Attr("reorderable", false)` hides the
  up/down buttons; a live region announces the row count after every change.
- **Schema diagnostics** — `BlazorFormDefinition.Validate()` reports duplicate sibling names, arrays
  with no item template, steps naming fields that do not exist, conditions pointing nowhere, and
  fields that belong to no wizard step.
- **Focus** — `BlazorFormState.FocusAsync(path)`, and `BlazorFormView.FocusFirstError` (on by default)
  takes the user to the first problem after a failed submit.
- **`BlazorFormView.SubmitAsync()` / `Reset()`** — the built-in submit pipeline is now callable from a
  custom `Actions` row or a button elsewhere on the page.

### Changed

- **Conditions are scoped to the object that owns the field.** A `VisibleWhen`/`DisabledWhen`/
  `RequiredWhen`/`MatchesField` written on a repeater's item template now means *this row*, matching
  how computed dependencies have always worked. Absolute paths still resolve, because the scope falls
  back to the root.
- **The library's own UI text goes through `IBlazorFormMessageProvider`** — buttons, the select
  placeholder, the repeater's labels and empty state, the error summary heading. `SubmitText`,
  `ResetText`, `BackText` and `NextText` are now nullable and fall back to the provider.
- Default CSS uses logical properties throughout (so RTL layouts are correct) and honours a host app's
  `data-theme="dark"`/`"light"` in addition to the OS preference.

- **JSON Schema `anyOf`/`oneOf`** — a null union (`["string"] | null`) collapses to the branch it wraps,
  and a `oneOf` of `const` values imports as a labelled select. A union of genuinely different object
  shapes is left alone rather than guessed at.
- **Full JSON round-trip for field presentation** — affixes, character counters, label suppression,
  suggestions (as `examples`), update trigger and debounce, autofocus, input mode, max file size,
  custom renderer keys and both renderer-hint bags now survive export and re-import.

- **`SeedMinItems()`** — a repeater opens with the rows its `MinItems` requires, instead of opening
  with an error about not having them. Opt-in, because on an edit form "this record has no lines" can
  be the truth.
- **`BlazorFormState.HasValidated`** — tells `IsValid` apart from "nothing has been checked yet".
- Wizard accessibility: the stepper is a labelled `<nav>` landmark that politely announces
  "Step 2 of 4", the form carries `aria-busy` while validating or submitting, and changing step moves
  focus to the new step's content — without which a keyboard or screen-reader user is left standing on
  the Next button with no indication that anything happened.

- **`SetValues(...)` and `Batch(...)`** — write many values as one change. Prefilling a form from a
  saved record otherwise cost one re-render per field and briefly exposed a half-applied state.

### Fixed

- **A file property no longer generates a text box.** `IBrowserFile`, `Stream` and collections of
  either fell through the type resolver to `Text`; they now resolve to a file field, with a collection
  becoming one multi-file control rather than a repeater of file pickers.
- `FocusAsync(path)` reports whether focus actually landed, so "go to the first error" walks past a
  field it cannot reach — one on another step, or a control with no focusable element — instead of
  stopping there and doing nothing. Radio and multi-select groups now offer the group as that target.
- An empty colour picker shows the colour it is actually displaying (black, or `Attr("emptyColor", …)`)
  instead of claiming to be empty while the browser renders a swatch.
- **A pristine form is no longer reported dirty.** Seeding computed values at construction marked their
  fields dirty, so any schema with a computed field enabled an "undo" button nobody had earned.
- **An external validator's rules no longer fire on hidden fields.** FluentValidation sees the whole
  model and knows nothing about the form's conditions, so a rule on a branch the user was never shown
  refused the submit and pointed at a control that was not on the page. The built-in rules have always
  skipped hidden fields; external ones now match.
- **`ClearOnHide` marks the form dirty.** The user did not type in the box, but the data about to be
  submitted is no longer what it was.
- **A UI step no longer exports as a `multipleOf` constraint.** `Step(0.01)` on a price meant the
  spinner's granularity, not a promise that 0.005 is invalid; it travels as `x-step` now, and only a
  real `MultipleOf(...)` rule exports as `multipleOf`.
- **Presentational fields are excluded from the data layer** — no defaults, no initial-value capture,
  no reset write, no validation. A `Static` heading has a name so the schema can address it, not
  because the model has a property of that name.
- **The error summary distinguishes repeater rows.** Every row shares one field definition, so five
  rows produced five identical "Product: required" entries; they now carry the row number, and each
  link moves focus for real instead of relying on a fragment jump.
- `OrderedMessages()` no longer walks the whole schema when there is nothing to order, the message
  provider is resolved once per form rather than per label, and a field's extra HTML attributes are
  built once rather than on every render.
- **A hidden wizard step no longer blocks submission.** Its fields were still validated on a full
  submit, producing errors on a page the user could never reach.
- **The wizard no longer strands the user on a step a condition has just hidden**; it falls back to the
  nearest visible step.
- **A new repeater row is seeded from its item template's defaults.** Defaults reached only top-level
  fields and object children before, so "quantity starts at 1" applied to no row at all.
- **Moving a repeater row moves its errors, touched and dirty state with it** instead of discarding
  every row's state, which is what insert and remove already did.
- **`Autofocus` actually focuses.** It was emitted as the HTML attribute, which browsers honour only on
  the initial page load — never on an interactively rendered Blazor form.
- `Reset()` cancels in-flight option loads, so a superseded lookup can no longer repopulate a select
  after the form has been reset.
- A radio or multi-select group with no visible label is named inline rather than pointing
  `aria-labelledby` at an element that was never rendered.

## [0.2.0]

### Added

- **Validation modes** — `BlazorFormState.ValidationTrigger` and `RevalidationTrigger` choose when a
  field revalidates before and after the first submit.
- **File upload** — `BlazorFormFieldType.File` renders `InputFile`, binds `IBrowserFile`, and is
  validated by `BlazorFormFileRule` (size and accepted types).
- **Error summary** — `<BlazorFormErrorSummary>` lists every error in schema order, links to each
  field, and takes focus after a failed submit. `BlazorFormView.ShowErrorSummary` opts in.
- **Localisation** — `IBlazorFormMessageProvider` replaces the built-in English messages;
  `AddBlazorFormMessages<T>()` registers one.
- **Async and cascading options** — `OptionsFrom(...)` loads a field's choices on demand and reloads
  them when a declared dependency changes, clearing a selection that no longer exists.
- **Computed values** — `Computed(...)` derives a field from the rest of the form. Formulas are
  evaluated against the object that owns the field, so a per-row total inside a repeater works
  without knowing its index; chained formulas cascade and circular ones settle.
- **Conditional requiredness** — `RequiredWhen(...)`, plus `ClearOnHide()` to empty a field when it
  disappears.
- **Form-level rules** — `MustAll(...)` and `BlazorFormDefinition.Validators`, with
  `BlazorFormRuleResult.FailFor` to attach the message to the field the user should fix.
- **New rules** — URL, compare ("confirm password"), multiple-of, unique items and file rules.
- **Layout** — `Columns(n)` on the form and `ColumnSpan(n)` on a field produce a responsive grid.
- **Read-only mode** — `BlazorFormView.ReadOnly` / `BlazorFormState.ReadOnly` for review screens.
- **State API** — `Reset()`, `AcceptChanges()`, `SubmitAsync()`, `MarkAllTouched()`,
  `OrderedMessages()`, `SetServerErrors(...)`, `InsertArrayItem(...)`, `IsSubmitting`, `IsSubmitted`,
  `DirtyFields`, `TouchedFields`, `VisibleSteps`, `CurrentStepNumber`, `GoToStep(id)`.
- **Typed builder** — nested member expressions (`x => x.Address.City`), typed `Object(...)` and
  `Array(...)` overloads, `When(...)` for conditional rules, and `BlazorFormExpressionPath` for
  refactor-safe field references in conditions and steps.
- **JSON Schema** — `$ref`/`$defs`, `allOf` merging, `if`/`then`/`else`, `dependentRequired`,
  `const`, exclusive bounds, `multipleOf`, `uniqueItems`, `readOnly`, nullable type unions and more
  formats. Conditions, wizard steps and layout now round-trip through `x-` extensions. `TryImport`
  reports malformed documents instead of throwing.
- **Generator options** — `ReadOnlyProperties`, `IgnoredProperties` and a `ConfigureField` hook;
  enum members honour `[Display]`/`[Description]`, and `[Flags]` enums become multi-selects.
- **Accessibility** — labelled controls, `aria-required` / `aria-invalid` / `aria-describedby`,
  grouped radio and multi-select controls, labelled repeater buttons, and a form that opts out of
  native browser validation.
- Dark-mode support in the default theme, and NuGet packaging metadata (README, repository, symbols).

### Changed

- **Rules no longer stack.** Constraints added through the fluent builder replace the equivalent rule
  generated from DataAnnotations instead of adding a second copy, so a field never reports the same
  problem twice.
- **`Required()` on a checkbox now means "must be ticked"**, matching HTML's own `required`.
- **Read-only is no longer rendered as disabled.** A read-only field keeps the `readonly` attribute
  and stays focusable; only `DisabledWhen` disables. Controls with no `readonly` attribute of their
  own (select, checkbox, radio) still fall back to `disabled`.
- **Fields render independently.** Editing one field no longer re-renders every other control.
- Wizard steps are numbered contiguously when a conditional step is hidden, completed steps are
  clickable, and a step may own a nested field path.
- Array messages, touched and dirty state follow their item through insert, remove and reorder.
- `BlazorFormView` rebuilds its state when `Definition`, `Model` or `Data` changes.

### Fixed

- Writing an unconvertible value no longer throws, and no longer disappears silently — it is
  reported as a validation message while the model keeps its last valid value.
- Writing into an empty typed collection creates an element of the declared type instead of throwing.
- Multi-select values are converted to the model's element type (including `[Flags]` enums).
- `TimeSpan` round-trips through `<input type="time">`; `DateTimeOffset` honours the field type.
- Numeric attributes (`min`, `max`, `step`) and numeric comparisons are culture-invariant.
- Regular expressions from a schema are compiled with a match timeout, and one that does not compile
  degrades to "no constraint" instead of throwing during import.
- Property lookup no longer throws `AmbiguousMatchException` on a shadowed property.
- Malformed form paths report a clear `FormatException`.
- Superseded validation runs and option loads are cancelled, so a slow async rule cannot overwrite a
  newer result.
- A `Uri`, `Guid` or `byte[]` property is no longer mistaken for a nested object group.
- An unregistered custom renderer key fails with a message naming the key rather than silently
  rendering a text box.
- `BlazorFormView.ReadOnly` is nullable, so setting it back to false actually takes effect while an
  unset value still leaves a caller-supplied state in charge of its own flag.

## [0.1.0]

- Initial release.
