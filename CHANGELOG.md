# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.3.0] — Unreleased

### Added

- **`ReadOnlyWhen(...)`** — the fourth conditional, and the one that was missing. Visibility, enabled
  and requiredness could all be decided by the data; whether a field was *locked* could not, so a
  settled answer — an invoice that has been sent, a field the approver owns — had to borrow
  `DisabledWhen`. That is not the same thing: a disabled control leaves the tab order and is not
  announced at all, so a value the user still needs to read off becomes one a keyboard or
  screen-reader user cannot reach. Read-only keeps it reachable, readable and copyable. Scoped to the
  owning object like every other condition, so it means *that row* inside a repeater; it round-trips
  through JSON as `x-readOnlyWhen`, and `Definition.Validate()` checks where it points.
- **`Normalize(...)` and `Trim()`** — tidy the value rather than complaining about it. A rule that
  rejects `" a@b.com "` for its surrounding spaces is technically correct and of no use to anyone: the
  user cannot see the difference, and trimming inside the rule fixes the message while leaving the
  untidy value in the model. Normalizers run when the user leaves the field and once more over every
  field on submit — so one nobody visited is tidied too — and never per keystroke, which would eat the
  space between two words the moment it was typed. `Trim()` turns whitespace-only into null, so a
  required field is not satisfied by `"   "`. React Hook Form spells this `setValueAs`.
- **`Range(...)` takes dates and times.** `Range(DateTime.Today, DateTime.Today.AddMonths(6))` and
  `Range(new TimeOnly(14, 0), new TimeOnly(22, 0))` say what they mean, instead of the caller having
  to convert to OLE automation numbers by hand and read a message that reported one back.
- **`IsSubmitSuccessful`** — whether the last submit passed validation *and* its handler returned
  without throwing. `IsValid` cannot do the job a "Saved." banner needs: it goes back to true the
  moment the user corrects their last error, long before anything is saved, and says nothing at all
  about whether the save then failed. React Hook Form spells it the same way.
- **`BlazorFormView.ConfigureState`** — configure the state the view builds for you, in place. Wanting
  one line ("validate on blur", "wire up this validator", "log a failed options lookup") otherwise
  meant giving up the `Definition` + `Model` shorthand entirely and constructing, holding and
  disposing a `BlazorFormState` by hand. Runs once per state, before the first render, and is ignored
  when a `State` is supplied — that one is already the caller's to configure.
- **`--ff-max-width`** — the form's width is a theme token like everything else, rather than a
  hard-coded rule to be fought with a more specific selector.
- **`BlazorFormView.SubmittingText`** — the submit button says what it is doing while validation or
  the save is in flight, instead of only going grey. A greyed-out button with an unchanged label reads
  as broken rather than busy, and told a screen-reader user nothing at all about why pressing it
  appeared to do nothing. Falls back to the message provider (`ui.submitting`).
- `Definition.Validate()` checks a wizard step's `VisibleWhen` dependencies, which were the one set of
  condition paths it never looked at. A dependency naming nothing reads as null forever, so the step is
  either always shown or never shown — and "never shown" on a wizard is a page of questions the user is
  never asked and a set of answers the schema quietly drops.
- **`IsValidatingField(path)` / `ValidatingFields`** — which field an async rule is currently checking,
  so a remote uniqueness lookup can put a spinner beside its own box instead of declaring the whole
  form busy and grey out a submit button that has nothing to do with it. Reported only for fields that
  actually have an async rule, so an ordinary form never flickers. Also carried on `GetFieldState`
  and on the renderer's field context. React Hook Form spells the set `validatingFields`; TanStack
  Form spells it `field.state.meta.isValidating`.

- **`RevalidateOn(...)`** — the other half of a cross-field rule. A rule that compares two values lives
  on one of them, so only one of the two changes ever runs it: the user mistypes the confirmation, is
  told the two do not match, then fixes the *password* to agree with what they typed — and the message
  under the confirmation is now false, and stays there until they go back and touch a field that was
  already right. Declaring what a field reads makes the correction land wherever it is made.
  `MatchesField(...)` and `[Compare]` register their own dependency, so a confirm-password field needs
  nothing extra. Paths resolve relative to the object that owns the field, so a rule on a repeater's
  template means that row, and a dependent that has not been touched on a form that has not been
  submitted is left alone — the point is to correct a verdict already on screen, never to bring one
  forward. React Hook Form spells it `deps`; TanStack Form spells it `onChangeListenTo`. It round-trips
  through JSON as `x-revalidateOn`, and `Definition.Validate()` reports a comparison rule that never
  revalidates.

- **Combobox (`AsCombobox()`)** — a text box that filters its options as the user types, for the
  dropdown that grew past a screenful. It is built on the browser's own `<input list>` + `<datalist>`,
  so filtering, keyboard navigation, touch behaviour and screen-reader support are the platform's
  rather than a thousand lines of hand-rolled ARIA; what the library adds is the one thing a bare
  datalist cannot do — mapping the label the user reads onto the value the model stores. It takes
  static options, `OptionsFromEnum`, and async/cascading `OptionsFrom` exactly as a `<select>` does.
  Closed by default (an answer on no list is reported, never discarded); `AsCombobox(allowCustom:
  true)` makes the list a shortcut instead of a constraint.
- **Tags (`AsTags()`)** — a collection of short strings as removable chips with one box to type the
  next into. The same data `ArrayOf(..., Text)` holds, with the affordance a word deserves: a repeater
  gives every entry a row with add, remove, duplicate and reorder buttons, which is right for an
  invoice line and wrong for "urgent". Enter or a separator commits, backspace on an empty box takes
  the last one back, duplicates are refused case-insensitively, and the entry box belongs to no form
  so Enter adds a tag instead of posting the page.
- **`BlazorFormState.Snapshot()`** — every path the schema binds to, mapped onto its value, including
  one entry per repeater row. It is exactly the shape `Reset(values)` takes back, so the pair is a
  complete save-and-restore for a draft: stash it while the user is halfway through, hand it back when
  they return.
- **`BlazorFormDefinition.Clone()`** — an independent copy of a schema. A definition is usually built
  once and kept, and every collection on it is mutable, so adding this user's options to a field edits
  the schema everyone else is looking at. Containers are rebuilt; rules, conditions and the delegates
  behind `Computed`, `OptionsProvider` and `OnChanged` are shared, having no state to corrupt.
- **`state.UseDataAnnotations()`** — runs the model's `IValidatableObject` alongside the schema's
  rules, the .NET-native counterpart of `UseFluentValidation()`. The generator already turns the
  property attributes it understands into field rules, so only the cross-property layer is added by
  default; running `Validator` over the properties again would put two differently-worded copies of
  the same complaint under every field. `IValidatableObject.Validate` is called directly rather than
  through `Validator.TryValidateObject`, which skips it entirely once any property attribute has
  failed — on a form that would mean the user fixes the last required field, submits again, and is
  told about something new.
- **`OnChange(handler)`** — the schema's own "and when this changes, do that": clear the city when the
  country changes, fill in the seat count when the plan does. Neither a computed value (which owns its
  field's value outright) nor a cascading options dependency (which reloads choices) can express it,
  because what the handler writes is still the user's to edit afterwards. TanStack Form calls these
  listeners; Formly calls them hooks. Paths resolve relative to the object that owns the field, so a
  handler on a repeater's template means *that row*, and handlers that answer each other settle rather
  than recursing.

- **`BlazorFormState.Reset(values)`** — rebases the form onto new data: the values are written, become
  the new baseline for `Reset()` and `IsFormDirty`, and everything the previous session accumulated is
  cleared. This is what an edit form needs after a save round-trip returns the stored record;
  `Reset()` would put back the values the form was *constructed* with, which are now the wrong ones.
  React Hook Form spells it `reset(values)`.
- **`ValidateFieldAsync(path)`** — validating one field no longer means finding its definition first.
  Returns false when the path names nothing in the schema, so a typo does not silently pass. React
  Hook Form spells it `trigger(name)`.
- **`GetFieldState(path)`** — touched, dirty, invalid, the first error and every message, in one read.
  A custom renderer asking all of them separately otherwise has to keep the answers in step itself.
- **`SwapArrayItems` / `ClearArrayItems`** — the two repeater operations that were missing. A swap
  exchanges two rows and leaves every other row where it was, which is what a drag-and-drop reorder
  means; `MoveArrayItem` shuffles everything in between along by one. Clearing empties the list as one
  change, rather than costing a re-render and a re-index per row.
- **`BlazorFormState.SingleErrorPerField`** — shows a field's first error instead of every rule it
  currently breaks, which is React Hook Form's default (`criteriaMode: "firstError"`). Every rule
  still runs, so the submit decision is unchanged; warnings sit alongside the error rather than
  competing with it. Off by default.
- **`BlazorFormView.TitleLevel`** — the form's title is rendered at the heading level the surrounding
  page needs.

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

- **A collection of enum members generates a multi-select, not a repeater.** A `List<TDay>` used to
  render as add-a-row, open-a-dropdown, pick-Monday, add-a-row, open-a-dropdown, pick-Tuesday — for a
  question with three possible answers. A `[Flags]` enum has always been a set of tick boxes and holds
  exactly the same information; so does the list. Arrays, `List<T>` and nullable elements all count,
  and the choices come from the element type. A list of anything that is *not* a closed set is still a
  repeater, which is what a repeater is for.
- **The same, for JSON Schema.** `{"type":"array","uniqueItems":true,"items":{"enum":[…]}}` imports as
  a multi-select — the rule react-jsonschema-form uses. Without `uniqueItems` the document is
  explicitly allowing a value twice, which only a repeater can express, so it stays one.

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

- **`AddBlazorForm(configure)` silently did nothing on a second call.** The registration is `TryAdd`,
  so an application registering its custom renderers after a component library or a shared module had
  already called `AddBlazorForm()` built a registry, configured it, discarded it, and was left with a
  renderer key that resolved to nothing — and a field that threw at render time naming a key the
  developer could see themselves registering. A later call now configures the registry that is there.
- **A multi-select lost its size bounds on a JSON round trip.** `minItems`, `maxItems` and
  `uniqueItems` were read only when the *mapped control* was a repeater — and a multi-select is
  `"type": "array"` carrying its choices as a top-level `enum`, which reads as a select until the
  widget hint corrects it. "Choose at least one" survived the export and was dropped on the way back
  in. The declared JSON type decides this now, not the control it renders as.
- **A second message on a field was recorded and never shown.** A field decides whether to redraw by
  comparing its message list's *identity* — the cheapest correct test, because a validation pass
  always rebuilds the list. Anything that appended to the list instead left the identity unchanged, so
  the field concluded it had nothing new to draw: a second `SetServerError` on a box that was already
  reporting something went into the state and never onto the page. Messages are now written
  copy-on-write, and the invariant the render check depends on holds everywhere.
- **A generated form was ordered by file layout, not by inheritance.** Property order came from
  metadata tokens alone, which run in declaration order within a file — so moving a base class below
  its derived class silently reversed the form, and a base class in another assembly produced an
  interleaving that meant nothing at all. Inherited fields now come first, root base class down, and
  `[Display(Order)]` still wins over both.
- **A required file field could be focused but not seen.** `<InputFile>` is a component rather than an
  element, so focus lands on the wrapper around it — and the wrapper was the one focusable container
  in the stylesheet with no focus ring. A keyboard user pressed submit, was told there was a problem,
  was moved to the file field, and had nothing on screen telling them where they now were.
- **Enter on a wizard step submitted the whole form.** Pressing Enter in a text box submits the form
  it belongs to — the browser does that whether or not a submit button is on the page — so a user
  halfway through a four-step wizard was answered with a validation pass over every step, and a wall
  of errors about questions they had not been asked yet. Enter now advances, exactly as the Next
  button does. `BlazorFormView.SubmitAsync()` still submits, because a caller asking for that has said
  so.
- **Debouncing did the waiting without ever running the check.** `UpdateOnInput(debounceMilliseconds:
  400)` is documented as the difference between a remote uniqueness check that is usable and one that
  is not — and then the write it performed skipped the async rules exactly as an undebounced keystroke
  does, so the check only ever ran on blur. A pause is what a debounce exists to detect, and it is now
  where the async rules run. An undebounced field is unchanged: it still waits for blur, because one
  request per character is the thing being avoided.
- **A repeater told only half the engine about itself.** Adding, removing, moving, duplicating or
  clearing rows changes the value the list binds to as surely as typing does, but only the recompute
  sweep ran: `VisibleWhen("Lines", IsNotEmpty)` with `ClearOnHide` never fired when the last line was
  deleted, a select loading its choices from the list never reloaded, and a wizard step conditional on
  the list was never re-clamped. Every row operation now runs the same sweeps every other change runs.
- **`SetServerErrors` threw away a conversion failure.** A value the model could never accept — `abc`
  in a field bound to an `int` — never reached the server, so the server has no opinion about it; but
  replacing the message set with the server's took its complaint with it, leaving the box showing text
  the model does not hold and nothing on the page to say why.
- **A date range was never enforced.** `[Range(typeof(DateTime), "2024-01-01", "2024-12-31")]` was
  read by the scanner, stored, and rendered as the input's `min`/`max` — and then the rule behind it
  skipped every date it was handed, because it could only compare numbers and a `DateTime` is not one.
  The form declared a window, the picker refused to leave it, and typing a date past the picker was
  accepted silently. Dates, times and timestamps are now compared on the same scale the bound is
  stored on, and a message about a date window names dates rather than the number `45292`.
- **A cascading select's cleared value told nobody.** Changing the country emptied the city, and that
  was the end of it: the *next* level of the chain kept a value chosen for the region it no longer
  belonged to, a computed total that read the cleared field kept its old figure, a `VisibleWhen`
  watching it never re-evaluated, the message describing the answer that had just been taken away
  stayed on screen, and an autosave listening to `OnFieldChanged` never saw it go. Emptying a
  dependent now runs the same sweeps a typed-in change does, which is what `ClearOnHide` has always
  done. A value written before its control ever rendered — a prefill, a restored draft — is cleared
  too, rather than surviving on the technicality that no option list had been fetched for it yet.
- **A combobox no longer tells assistive technology that its list is closed.** An `<input list>` is
  already a combobox in the accessibility tree, so the hand-written `role="combobox"` added nothing
  and took on `aria-expanded`'s contract — state only the browser has, and which was therefore
  hard-coded to `"false"`: an assertion that the popup is shut, made to a screen-reader user at the
  exact moment it is open. Both attributes are gone and the platform's own semantics stand.
- **`UseDataAnnotations()` and `UseFluentValidation()` no longer cancel each other out.** A form has
  one external-validator slot and every integration assigned to it, so wiring up both — which is a
  perfectly reasonable thing to want, since they cover different rules — meant whichever line came
  second silently threw the first away. They combine now, and a complaint both layers report in the
  same words on the same field is still read once. `BlazorFormExternalValidator.CombineWith(...)` does
  the same for a hand-written one.
- **A file field can be focused.** `InputFile` is a component rather than an element, so there was no
  focus target to register — which made a required file the single field type that "go to the first
  error" and the error summary's link silently walked past, on a form whose only problem was the
  missing file. The wrapper takes focus, exactly as a radio group's does.
- **A switch is visible in Windows High Contrast Mode.** Its on/off state was drawn entirely in a
  background colour, which forced-colors replaces — so checked and unchecked came out identical and
  the state was conveyed by a colour the mode had just taken away. The checkbox gets its native
  rendering back there, and an invalid field is marked by a dashed border rather than a hue.
- **A repeater operation revalidates what depends on the list.** Adding, removing, moving or
  duplicating a row changes the value the field binds to, but a repeater has no field context to route
  through, so a rule elsewhere declaring `RevalidateOn` on the list was judged once and never again.
- **Clearing a hidden field is a change like any other.** `ClearOnHide` emptied the value and told
  nobody: a total that read the field kept the number it had just thrown away, a cascading select that
  depended on it kept options built for a value no longer in the model, and `OnFieldChanged` never
  fired — so an autosave saved a field the form had already emptied. It now runs the same three sweeps
  a typed-in value does, and a chain of conditions that clear each other settles rather than
  recursing. This is the same fix a computed value needed.
- **A multi-select or tag list is no longer dirty for ever.** Both write a brand-new collection on
  every edit, and the baseline was held by reference — so the field was reported changed from the
  first click and never clean again, whatever the user did. An "undo" button, an unsaved-changes
  prompt and a disabled save button all read that. Collections are baselined element by element now,
  as a repeater's rows already were, so ticking a box and unticking it leaves the form clean. A
  `[Flags]` multi-select is unaffected: it renders as boxes but stores one value, which compares
  perfectly well as itself.
- **A multi-select stores its values in the schema's order, not the order they were clicked.** Ticking
  A then B and ticking B then A produced different data for the same answer, which made the previous
  bug's "put it back exactly as it was" impossible even in principle.
- **`ResetField(path)` runs the change sweep.** Undoing one answer skipped everything a typed-in value
  does except recomputation, so a cascading select kept the options loaded for the value that was just
  undone, a branch the answer was holding open stayed open, and nothing watching `OnFieldChanged`
  heard about it.
- **`Reset()` restores a collection the user emptied.** Putting a snapshot back needed a live list to
  refill, so a field cleared to null — by `ClearOnHide`, or by a clear button — was left empty by the
  reset that was supposed to bring its contents back.
- **The error summary is not announced twice.** It carried `role="alert"` *and* took focus, and a
  screen reader announces a live region that receives focus once for the region changing and again for
  the focus arriving — some read it more times than that. The role is now used only when the summary
  does not take focus itself, which is the case where nothing else would say anything at all.
- **An unlabelled field is still named in the error summary.** A hand-built schema need not label
  every field, and "This field is required." on its own names nothing — which is the one job a summary
  entry has. The field's name is used when there is no label.
- An external validator's results are filtered against the hidden fields computed once per pass rather
  than once per message; fifty failures on a two-hundred-field schema were re-evaluating every
  condition in the form fifty times to answer a question whose answer had not changed.
- **A combobox no longer stores a half-typed label as its value.** `UpdateOnInput()` on one wrote
  every keystroke straight through, bypassing the label-to-value mapping — so "Fran" became the
  country, and then "Franc", each flagged in turn as an answer that is on no list. A combobox now
  always commits when the choice is made, and `Definition.Validate()` reports a schema that asked for
  anything else — as it now does for every control that can only ever commit (select, radio,
  multi-select), where the setting was accepted and quietly ignored.
- **A repeater operation is reported through `OnFieldChanged`.** Adding, removing, moving, swapping,
  duplicating and clearing rows all changed the value of the field the list binds to and none of them
  said so, so an autosave or a live preview watching for changes missed every one. Duplicating
  announces itself once, and only once the copy is actually in the row — the insert underneath it
  stays quiet, because a listener reacting to that would have saved a blank line.
- **A dictionary-backed form matches its keys without regard to case**, as every other way a path is
  resolved already does — a schema lookup, a property on a typed model, the state's own touched, dirty
  and message tracking. It was the one place where `Email` and `email` were different fields, which is
  a distinction the library cannot honour anywhere else and which `Definition.Validate()` already
  reports as two siblings binding to the same path.
- **A length limit on a tag list is enforced.** `MinLength`/`MaxLength` read the value as a string,
  found a collection, and passed — so the limit the entry box enforced while typing was never checked
  on a value that arrived any other way. They now measure each tag. This is the same shape of bug as
  `[MinLength]` on a collection being mapped to the string rule, and a repeater's items are still the
  collection-size rule's business rather than this one's.
- **A tag list is labelled through its group.** The field's `<label for>` pointed at the box a tag is
  typed into — which disappears once the list is full or the form is read-only, leaving the label
  aimed at nothing. It is now named the way a radio group is, and the group carries the field's id so
  the error summary's link still lands.
- `Definition.Validate()` reports two combobox options sharing a label. The entry is matched back to
  an option by its label, because that is what the browser puts in the box, so two of them make the
  user's choice a coin toss.
- **A computed value now behaves like any other change.** Writing one skipped the three sweeps a
  typed-in value runs, so a field whose `VisibleWhen` read a total was never cleared when the total
  hid it, a cascading select that depended on a computed field never reloaded, and `OnFieldChanged`
  never heard about it at all — the schema's own calculations were the one kind of change the rest of
  the engine could not see.
- **A read-only wizard no longer offers to submit itself.** The single-page branch has always hidden
  its buttons in review mode; the wizard branch rendered Submit and Reset regardless. Back and Next
  stay — reading a form is not editing it, and a review screen that strands the user on step 1 of 4 is
  worse than the button it was hiding. `ShowSubmitButton="false"` is now honoured on a wizard too.
- **`InputAttr(...)` reaches radio, multi-select and file fields.** Those three renderers never
  splatted the schema's extra attributes, so a `data-testid` or a `title` declared on them silently
  did nothing. They land on the group element, which is the only thing there is to put them on, and
  the renderer's own ARIA wiring still wins.
- **`HideLabel()` works on a checkbox and a switch.** It was the one field type that rendered its
  label anyway; the visible text now goes and the label survives as the control's accessible name,
  exactly as it does everywhere else.
- A multi-select rebuilt its selection set once per option rather than once per render, so a list of
  200 choices walked the stored value 200 times to draw 200 boxes.
- `HasErrors` — read by every button on every render through `IsValid` — no longer allocates a pair of
  LINQ iterators and a closure to answer it.
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
