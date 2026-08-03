# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.3.0] — Unreleased

### Added

- **Named field groups** — a run of consecutive fields sharing a `Group(...)` name (or
  `[Display(GroupName = …)]`) renders as one `<fieldset>` with a `<legend>`, so the grouping exists for
  a screen reader as well as for the eye. The name round-trips through JSON as `x-group`.
- **`<BlazorFormField State="…" Name="…" />`** — places one field of a schema wherever the page wants
  it, so the layout can be the page's while labels, validation, conditions and the ARIA wiring stay the
  schema's.
- **`BlazorFormState.ResetField(path)`** — puts one field, and anything nested beneath it, back to the
  value it started with. `Reset()` is far too blunt when the user changed their mind about one answer.
- **Options failures are survivable** — `BlazorFormState.OptionsError(path)` and the
  `OptionsLoadFailed` event report a provider that threw, and the control says so instead of showing an
  empty dropdown. Previously the exception escaped into the renderer's `OnParametersSetAsync` and took
  the component down.
- **The character counter is announced.** The visible counter stays `aria-hidden`, and a polite live
  region announces the remaining count as the limit comes into view (threshold configurable with
  `Attr("countAnnounceAt", n)`). Without it a screen-reader user met the limit by having their typing
  silently stop working.
- `Definition.Validate()` reports more than one field asking for `Autofocus`, which is a race the
  schema cannot win; bounds that cross over (`Range(10, 5)`, `Items(min: 4, max: 2)`), which describe a
  field no value can satisfy; and a computed field left editable, whose input is overwritten the next
  time a dependency changes.
- **`BlazorFormState.Disabled` / `BlazorFormView.Disabled`** — disables every control and every button
  at once, for a save in flight or a locked record. Distinct from `ReadOnly`, which stays focusable and
  readable, and it does not claim the form is `aria-busy`.
- **Repeater focus management** — adding, duplicating or removing a row moves focus somewhere sensible
  (into the new row, into the row that took a deleted one's place, or onto the add button when the list
  empties) instead of leaving it on `<body>`.
- **`IBlazorFormDataReader.TryGetValue`** — reports whether a path exists, as distinct from whether it
  holds anything. A default implementation keeps existing readers working.
- An option's `Group` (its `<optgroup>`) and `Disabled` flag round-trip through JSON as `x-enumGroups`
  and `x-enumDisabled`, instead of being dropped on export.
- **`BlazorFormState.FurthestStepIndex` / `IsStepReachable(index)`** — the stepper now offers every step
  the user has already walked past, forwards as well as back. Coming back to fix one answer no longer
  means pressing Next through the rest of the wizard.
- **`BlazorFormFieldType.Search` / `AsSearch()`** — browsers give a search box a clear affordance, a
  search key on the on-screen keyboard and history from previous searches. The member is appended to
  the enum, so no existing one's numeric value moves.
- `Definition.Validate()` reports a `MatchesField` pointing at a path that is not in the schema. The
  other value reads as null, this one is compared against it, and the field passes for the wrong reason.
- **Each repeater row is a named group.** Every row repeats the same field labels, so a screen reader
  announced "Product, edit" once per line with nothing to tell them apart; the row now carries an
  accessible name ("line 2") through the message provider.

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

- **An unanswered checkbox now counts as false.** `IsTrue` and `IsFalse` were both false for a missing
  value, so the two branches of one yes/no question could hide a field under each — which a
  dictionary-backed form met immediately, since an untouched checkbox has no entry at all while a
  `bool` on a typed model already reads false. A non-empty value that is not a boolean is still
  neither.
- **Two options whose values differ only in punctuation no longer share a DOM id.** Folding
  non-id characters to `_` made `en-US` and `en_US` the same id, so one `<label for>` pointed at the
  other's control and clicking a label ticked the wrong box.
- **A repeater's reorder arrows no longer drop focus.** Moving a row to either end disabled the very
  button that moved it, and a `disabled` button leaves the tab order — so a keyboard user was returned
  to the page body mid-reorder. They are `aria-disabled` now, and the click is guarded instead.
- **Clicking a completed step in the stepper moves focus to that step's content**, as Back and Next
  already did. Without it the content below is replaced with no indication that anything happened.
- **Deleting a repeater row no longer leaves the rows below it bound to the wrong index.** The
  render-skip optimisation compares what a field *displays*, which does not include the path it
  displays it from — so a row Blazor reused at a new index looked unchanged, the render that would
  apply the new path was suppressed, and the surviving rows carried on rendering (and writing to) the
  indices they used to have. The last row bound to an element that no longer existed. The same applied
  to an insert anywhere but the end.
- **`[MinLength]` / `[MaxLength]` on a collection are enforced.** They mean item counts, exactly as
  `[Length]` does, but were mapped to the string rule — which reads the value as a string, finds a
  `List<T>`, and passes. `[MinLength(1)]` on a list silently allowed an empty one.
- **A row's own empty field is no longer answered for by a root field of the same name.** A scoped read
  fell back to the root whenever the scoped value came out null, so `VisibleWhen("Email", IsEmpty)`
  inside a repeater read the model's top-level `Email` for any row that had not filled one in. The
  fallback now triggers on the path being absent rather than the value being empty, so the case it was
  written for — an absolute reference from inside a row — still works.
- **A field with a live character counter no longer carries `maxlength`.** The attribute is
  destructive: pasting a slightly-too-long answer silently truncated it, with no message and nothing to
  undo, which is the opposite of what showing a count is for.
- A range input with an affix announces `aria-valuetext` ("70 kg"), not just the bare number.
- **Dirtiness is a comparison, not a flag.** Every write marked its field dirty and nothing ever
  cleared it, so typing a character and deleting it again left the form reporting unsaved work — and an
  undo button, an unsaved-changes prompt and a disabled save button all read that. Values are now
  compared against the baseline, as `dirtyFields` is in React Hook Form and TanStack Form, so a row
  added and removed again also leaves the list clean.
- **Cascading options match their dependencies the way everything else does.** `OptionsFrom(...,
  dependsOn: "Country")` compared paths for exact equality, so a cascading select inside a repeater
  row never reloaded (its sibling is `Rows[0].Country`, not `Country`) and naming a container never
  fired. Dependencies now resolve relative to the owning object and by prefix, matching conditions and
  computed values — and matching what the README already claimed.
- **`ValidateStepAsync` supersedes older runs.** Unlike `ValidateAsync` it had no cancellation and did
  not raise `IsValidating`, so two quick "Next" clicks could let a stale verdict land last and the
  buttons stayed enabled while a step's async rules ran.
- **The `<form>` splats caller attributes first**, as every input renderer already did. Splatting last
  let a caller-supplied `class` erase `ff-form` and a stray `onsubmit` unhook validation.
- **A form-level message leads the error summary, and appears once.** It sorted below every field error
  despite naming no control, and was rendered a second time in the view's own form-level block.
- **A repeater whose list holds the same object twice no longer throws.** The row's identity was used
  as its `@key` unconditionally, and a duplicate key is an exception rather than a rendering quirk.
- **A required `<select>` no longer offers its placeholder back as an answer.**
- **A nested object group's help text is announced with the group** — it was rendered with an id that
  nothing pointed `aria-describedby` at, leaving it visible but silent.
- **`Group` is no longer dead API.** It was declared, documented and populated from
  `[Display(GroupName = …)]`, and never rendered.
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
